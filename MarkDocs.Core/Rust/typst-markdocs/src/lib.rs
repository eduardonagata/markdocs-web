//! `typst-markdocs` — a fronteira entre o .NET e o motor Typst.
//!
//! Transcrição do crate `typst-notiq` do NotiqTypst: mesma forma, mesmas
//! regras, só os nomes trocam. Uma função, dados brutos, um par
//! alocar/liberar. As três regras que esta camada tem de cumprir:
//!
//! 1. Nenhum ponteiro do Rust sobrevive à chamada — o .NET copia e chama
//!    `markdocs_typst_liberar`.
//! 2. Nenhuma falha do Rust derruba o processo — `catch_unwind` embrulha o
//!    corpo inteiro e devolve `CODIGO_FALHA_INTERNA`.
//! 3. Nenhum acesso a disco ou rede — o `MundoMarkDocs` responde de um
//!    `HashMap` e não existe caminho de código que abra um arquivo.

use std::collections::HashMap;
use std::ffi::{c_char, CStr};
use std::panic::{catch_unwind, AssertUnwindSafe};

use typst::diag::{FileError, FileResult, Severity, SourceDiagnostic};
use typst::foundations::{Bytes, Datetime, Duration, Smart};
use typst::text::{Font, FontBook};
use typst_layout::PagedDocument;
use typst::utils::LazyHash;
use typst::{Library, LibraryExt, World, WorldExt};
use typst_pdf::{PdfOptions, PdfStandards};
use typst_syntax::{DiagSpanKind, FileId, RootedPath, Source, VirtualPath, VirtualRoot};

// ───────────────────────────────── códigos de retorno

const CODIGO_OK: i32 = 0;
/// O `.typ` não compilou. `diagnostico` traz o JSON com arquivo, linha e coluna.
const CODIGO_ERRO_DE_COMPILACAO: i32 = 1;
/// O Rust entrou em pânico. É defeito nosso, e vira `ErroTypst.FalhaInterna`.
const CODIGO_FALHA_INTERNA: i32 = 2;
/// A entrada não é utilizável: ponteiro nulo, caminho inválido, UTF-8 quebrado.
const CODIGO_ENTRADA_INVALIDA: i32 = 3;

// ───────────────────────────────── a fronteira em C

#[repr(C)]
pub struct MarkDocsArquivo {
    pub caminho: *const c_char,
    pub dados: *const u8,
    pub tamanho: usize,
}

#[repr(C)]
pub struct MarkDocsFonte {
    pub dados: *const u8,
    pub tamanho: usize,
}

#[repr(C)]
pub struct MarkDocsEntrada {
    pub arquivos: *const MarkDocsArquivo,
    pub arquivos_len: usize,
    pub entrada: *const c_char,
    pub fontes: *const MarkDocsFonte,
    pub fontes_len: usize,
    /// Segundos desde 1970. Torna `today()` determinístico.
    pub instante: i64,
}

#[repr(C)]
pub struct MarkDocsSaida {
    pub pdf: *mut u8,
    pub pdf_len: usize,
    pub diagnostico: *mut c_char,
}

/// Compõe o Acervo recebido e devolve os bytes do PDF.
///
/// # Safety
/// `entrada` e `saida` precisam apontar para structs válidas, e os ponteiros
/// dentro de `entrada` precisam permanecer vivos durante a chamada.
#[no_mangle]
pub unsafe extern "C" fn markdocs_typst_compilar(
    entrada: *const MarkDocsEntrada,
    saida: *mut MarkDocsSaida,
) -> i32 {
    if entrada.is_null() || saida.is_null() {
        return CODIGO_ENTRADA_INVALIDA;
    }

    // A saída nasce zerada: `markdocs_typst_liberar` é seguro mesmo se sairmos
    // por um caminho de erro antes de preencher qualquer coisa.
    unsafe {
        (*saida).pdf = std::ptr::null_mut();
        (*saida).pdf_len = 0;
        (*saida).diagnostico = std::ptr::null_mut();
    }

    // Regra 2: o corpo inteiro embrulhado. Um pânico vira código de retorno.
    let resultado =
        catch_unwind(AssertUnwindSafe(|| unsafe { compilar(&*entrada, &mut *saida) }));

    match resultado {
        Ok(codigo) => codigo,
        Err(panico) => {
            let mensagem = descrever_panico(&panico);
            unsafe {
                (*saida).diagnostico = texto_para_c(&diagnostico_avulso(
                    "falhaInterna",
                    &mensagem,
                ));
            }
            CODIGO_FALHA_INTERNA
        }
    }
}

/// Libera o que `markdocs_typst_compilar` alocou. Roda num `finally`, do lado .NET.
///
/// # Safety
/// `saida` precisa ter vindo de `markdocs_typst_compilar` e não pode ser liberada
/// duas vezes.
#[no_mangle]
pub unsafe extern "C" fn markdocs_typst_liberar(saida: *mut MarkDocsSaida) {
    if saida.is_null() {
        return;
    }
    unsafe {
        let s = &mut *saida;
        if !s.pdf.is_null() {
            drop(Vec::from_raw_parts(s.pdf, s.pdf_len, s.pdf_len));
            s.pdf = std::ptr::null_mut();
            s.pdf_len = 0;
        }
        if !s.diagnostico.is_null() {
            drop(std::ffi::CString::from_raw(s.diagnostico));
            s.diagnostico = std::ptr::null_mut();
        }
    }
}

// ───────────────────────────────── o corpo

unsafe fn compilar(entrada: &MarkDocsEntrada, saida: &mut MarkDocsSaida) -> i32 {
    let mundo = match unsafe { MundoMarkDocs::montar(entrada) } {
        Ok(mundo) => mundo,
        Err(motivo) => {
            saida.diagnostico = texto_para_c(&diagnostico_avulso("entradaInvalida", &motivo));
            return CODIGO_ENTRADA_INVALIDA;
        }
    };

    let composicao = typst::compile::<PagedDocument>(&mundo);
    let avisos = composicao.warnings;

    let documento = match composicao.output {
        Ok(documento) => documento,
        Err(erros) => {
            saida.diagnostico = texto_para_c(&diagnosticos_em_json(&mundo, &erros, &avisos));
            comemo::evict(0);
            return CODIGO_ERRO_DE_COMPILACAO;
        }
    };

    let opcoes = PdfOptions {
        // `ident` estável: o mesmo Markdown produz o mesmo PDF, byte a byte,
        // que é o que torna o teste de ouro possível.
        ident: Smart::Custom("markdocs".to_string()),
        creator: Smart::Custom(Some("MarkDocs".to_string())),
        timestamp: None,
        page_ranges: None,
        standards: PdfStandards::default(),
        tagged: true,
        pretty: false,
    };

    let bytes = match typst_pdf::pdf(&documento, &opcoes) {
        Ok(bytes) => bytes,
        Err(erros) => {
            saida.diagnostico = texto_para_c(&diagnosticos_em_json(&mundo, &erros, &avisos));
            comemo::evict(0);
            return CODIGO_ERRO_DE_COMPILACAO;
        }
    };

    // O `Vec` é entregue cru ao .NET, que copia para um `byte[]` e devolve o
    // ponteiro a `markdocs_typst_liberar`. Regra 1.
    let mut bytes = bytes;
    bytes.shrink_to_fit();
    let tamanho = bytes.len();
    saida.pdf = bytes.as_mut_ptr();
    saida.pdf_len = tamanho;
    std::mem::forget(bytes);

    if !avisos.is_empty() {
        saida.diagnostico = texto_para_c(&diagnosticos_em_json(&mundo, &[], &avisos));
    }

    // Mantém o cache incremental entre composições sucessivas — o PDF é o
    // *preview*, então compor acontece muitas vezes seguidas — sem deixá-lo
    // crescer sem limite.
    comemo::evict(4);

    CODIGO_OK
}

// ───────────────────────────────── o Acervo, do lado do Rust

struct MundoMarkDocs {
    biblioteca: LazyHash<Library>,
    catalogo: LazyHash<FontBook>,
    fontes: Vec<Font>,
    arquivos: HashMap<FileId, Bytes>,
    codigos: HashMap<FileId, Source>,
    entrada: FileId,
    hoje: Datetime,
}

impl MundoMarkDocs {
    unsafe fn montar(entrada: &MarkDocsEntrada) -> Result<Self, String> {
        let mut arquivos = HashMap::new();
        let mut codigos = HashMap::new();

        if entrada.arquivos.is_null() && entrada.arquivos_len > 0 {
            return Err("lista de arquivos nula".into());
        }

        let lista = if entrada.arquivos_len == 0 {
            &[][..]
        } else {
            unsafe { std::slice::from_raw_parts(entrada.arquivos, entrada.arquivos_len) }
        };

        for arquivo in lista {
            let caminho = unsafe { texto_de_c(arquivo.caminho) }
                .ok_or_else(|| "caminho de arquivo inválido".to_string())?;
            let id = identificar(&caminho)?;

            let dados: Vec<u8> = if arquivo.tamanho == 0 {
                Vec::new()
            } else if arquivo.dados.is_null() {
                return Err(format!("dados nulos para {caminho}"));
            } else {
                unsafe { std::slice::from_raw_parts(arquivo.dados, arquivo.tamanho).to_vec() }
            };

            // Todo `.typ` também vira `Source`: é o que o motor pede em
            // `source()`. Os demais existem só como bytes (o timbre, por ex.).
            if caminho.ends_with(".typ") {
                let texto = String::from_utf8(dados.clone())
                    .map_err(|_| format!("{caminho} não é UTF-8 válido"))?;
                codigos.insert(id, Source::new(id, texto));
            }

            arquivos.insert(id, Bytes::new(dados));
        }

        let caminho_de_entrada = unsafe { texto_de_c(entrada.entrada) }
            .ok_or_else(|| "caminho de entrada inválido".to_string())?;
        let id_de_entrada = identificar(&caminho_de_entrada)?;

        if !codigos.contains_key(&id_de_entrada) {
            return Err(format!("a entrada {caminho_de_entrada} não está no acervo"));
        }

        let mut fontes = Vec::new();
        if entrada.fontes_len > 0 {
            if entrada.fontes.is_null() {
                return Err("lista de fontes nula".into());
            }
            let lista = unsafe { std::slice::from_raw_parts(entrada.fontes, entrada.fontes_len) };
            for fonte in lista {
                if fonte.dados.is_null() || fonte.tamanho == 0 {
                    continue;
                }
                let dados = unsafe { std::slice::from_raw_parts(fonte.dados, fonte.tamanho) };
                // `iter` em vez de `new`: um arquivo pode ser uma coleção com
                // várias faces, e todas precisam entrar no catálogo.
                fontes.extend(Font::iter(Bytes::new(dados.to_vec())));
            }
        }

        if fontes.is_empty() {
            return Err("nenhuma fonte embarcada foi entregue ao motor".into());
        }

        let catalogo = FontBook::from_fonts(&fontes);

        Ok(Self {
            biblioteca: LazyHash::new(Library::default()),
            catalogo: LazyHash::new(catalogo),
            fontes,
            arquivos,
            codigos,
            entrada: id_de_entrada,
            hoje: data_de(entrada.instante),
        })
    }
}

impl World for MundoMarkDocs {
    fn library(&self) -> &LazyHash<Library> {
        &self.biblioteca
    }

    fn book(&self) -> &LazyHash<FontBook> {
        &self.catalogo
    }

    fn main(&self) -> FileId {
        self.entrada
    }

    fn source(&self, id: FileId) -> FileResult<Source> {
        self.codigos.get(&id).cloned().ok_or_else(|| nao_existe(id))
    }

    fn file(&self, id: FileId) -> FileResult<Bytes> {
        self.arquivos.get(&id).cloned().ok_or_else(|| nao_existe(id))
    }

    fn font(&self, indice: usize) -> Option<Font> {
        self.fontes.get(indice).cloned()
    }

    /// Valor fixo, de propósito: o mesmo Markdown produz sempre o mesmo PDF
    ///. Nenhum template deve usar isto — data no papel vem
    /// escrita no Markdown.
    fn today(&self, _fuso: Option<Duration>) -> Option<Datetime> {
        Some(self.hoje)
    }
}

/// Qualquer caminho fora do Acervo *não existe*. É a regra 3 dita em código:
/// não há consulta ao disco a fazer quando a resposta não está no dicionário.
fn nao_existe(id: FileId) -> FileError {
    FileError::NotFound(id.vpath().get_with_slash().into())
}

/// Todo caminho do Acervo é absoluto e mora na raiz do projeto. Não há pacotes
/// Typst: o *downloader* não é compilado, e `VirtualRoot::Package` nunca
/// aparece.
fn identificar(caminho: &str) -> Result<FileId, String> {
    let vpath = VirtualPath::new(caminho).map_err(|erro| format!("{caminho}: {erro:?}"))?;
    Ok(FileId::new(RootedPath::new(VirtualRoot::Project, vpath)))
}

// ───────────────────────────────── diagnóstico

fn diagnosticos_em_json(
    mundo: &MundoMarkDocs,
    erros: &[SourceDiagnostic],
    avisos: &[SourceDiagnostic],
) -> String {
    let itens: Vec<serde_json::Value> = erros
        .iter()
        .chain(avisos.iter())
        .map(|diagnostico| {
            let (arquivo, linha, coluna) = localizar(mundo, diagnostico);
            let severidade = match diagnostico.severity {
                Severity::Error => "erro",
                Severity::Warning => "aviso",
            };
            let dicas: Vec<String> =
                diagnostico.hints.iter().map(|dica| dica.v.to_string()).collect();
            serde_json::json!({
                "severidade": severidade,
                "arquivo": arquivo,
                "linha": linha,
                "coluna": coluna,
                "mensagem": diagnostico.message.to_string(),
                "dicas": dicas,
            })
        })
        .collect();

    serde_json::to_string(&itens).unwrap_or_else(|_| "[]".to_string())
}

/// Traduz o span numa posição legível. Span destacado vira `(none, 0, 0)` —
/// é o caso de um erro que não aponta para arquivo nenhum.
fn localizar(mundo: &MundoMarkDocs, diagnostico: &SourceDiagnostic) -> (Option<String>, usize, usize) {
    let id = match diagnostico.span.get() {
        DiagSpanKind::Detached => return (None, 0, 0),
        DiagSpanKind::Number { id, .. } => id,
        DiagSpanKind::Range { id, .. } => id,
    };

    let caminho = id.vpath().get_with_slash().to_string();

    let Some(faixa) = mundo.range(diagnostico.span) else {
        return (Some(caminho), 0, 0);
    };
    let Ok(codigo) = mundo.source(id) else {
        return (Some(caminho), 0, 0);
    };

    let (linha, coluna) = codigo.lines().byte_to_line_column(faixa.start).unwrap_or((0, 0));

    (Some(caminho), linha + 1, coluna + 1)
}

/// Um diagnóstico que não vem do motor: falha interna ou entrada inválida.
/// Mesma forma dos demais, para o lado .NET ter um só decodificador.
fn diagnostico_avulso(severidade: &str, mensagem: &str) -> String {
    serde_json::to_string(&serde_json::json!([{
        "severidade": severidade,
        "arquivo": serde_json::Value::Null,
        "linha": 0,
        "coluna": 0,
        "mensagem": mensagem,
        "dicas": Vec::<String>::new(),
    }]))
    .unwrap_or_else(|_| "[]".to_string())
}

fn descrever_panico(panico: &(dyn std::any::Any + Send)) -> String {
    if let Some(texto) = panico.downcast_ref::<&str>() {
        (*texto).to_string()
    } else if let Some(texto) = panico.downcast_ref::<String>() {
        texto.clone()
    } else {
        "pânico sem mensagem no motor Typst".to_string()
    }
}

// ───────────────────────────────── utilidades

unsafe fn texto_de_c(ponteiro: *const c_char) -> Option<String> {
    if ponteiro.is_null() {
        return None;
    }
    unsafe { CStr::from_ptr(ponteiro) }.to_str().ok().map(str::to_string)
}

fn texto_para_c(texto: &str) -> *mut c_char {
    match std::ffi::CString::new(texto) {
        Ok(c) => c.into_raw(),
        Err(_) => std::ptr::null_mut(),
    }
}

/// Segundos desde 1970 em ano/mês/dia (UTC), pelo algoritmo civil de Hinnant.
/// Não depende de fuso, de locale nem do relógio da máquina.
fn data_de(instante: i64) -> Datetime {
    let dias = instante.div_euclid(86_400);
    let z = dias + 719_468;
    let era = z.div_euclid(146_097);
    let doe = z.rem_euclid(146_097);
    let yoe = (doe - doe / 1_460 + doe / 36_524 - doe / 146_096) / 365;
    let ano = yoe + era * 400;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let dia = (doy - (153 * mp + 2) / 5 + 1) as u8;
    let mes = (if mp < 10 { mp + 3 } else { mp - 9 }) as u8;
    let ano = (if mes <= 2 { ano + 1 } else { ano }) as i32;

    Datetime::from_ymd(ano, mes, dia).unwrap_or_else(|| {
        Datetime::from_ymd(2026, 1, 1).expect("1º de janeiro de 2026 é uma data válida")
    })
}
