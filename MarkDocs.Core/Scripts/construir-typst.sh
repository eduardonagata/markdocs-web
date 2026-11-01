#!/bin/bash
#
# Constrói o motor Typst (Rust/typst-markdocs) e o coloca em
# Artefatos/runtimes/<rid>/native/, onde o MarkDocs.Core o procura.
#
# NINGUÉM PRECISA RODAR ISTO PARA TRABALHAR NO APP EM LINUX. A fatia
# linux-x64 está versionada no repositório (é a do Docker). Rode este script:
#
#   - para trabalhar num Mac (gera a fatia osx-arm64 ou osx-x64 da máquina);
#   - para SUBIR A VERSÃO DO TYPST, que é tarefa própria, nunca efeito
#     colateral de outra. Ela inclui, na ordem:
#       1. mudar a versão pinada em Rust/typst-markdocs/Cargo.toml (a mesma do
#          NotiqTypst, para os dois pacotes continuarem iguais);
#       2. rodar este script em Linux (x64) e commitar o .so novo;
#       3. migrar Sources/MarkDocs.Core/Nucleo/Emissao/ se a sintaxe mudou;
#       4. rodar os testes e comparar PDFs visualmente (nunca por bytes — o
#          PDF muda entre versões do motor mesmo com a página idêntica).
#
# Pré-requisito, uma vez por máquina:
#
#   curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y
#
# Uso:
#
#   MarkDocs.Core/Scripts/construir-typst.sh            # o RID desta máquina
#   MarkDocs.Core/Scripts/construir-typst.sh linux-x64  # um RID explícito
#                                                       # (alvo Rust instalado)

set -euo pipefail

cd "$(dirname "$0")/.."
RAIZ="$PWD"
CRATE="$RAIZ/Rust/typst-markdocs"

export PATH="$HOME/.cargo/bin:$PATH"

rid_da_maquina() {
    local so arq
    case "$(uname -s)" in
        Linux) so=linux ;;
        Darwin) so=osx ;;
        *) echo "sistema não suportado: $(uname -s)" >&2; exit 1 ;;
    esac
    case "$(uname -m)" in
        x86_64|amd64) arq=x64 ;;
        arm64|aarch64) arq=arm64 ;;
        *) echo "arquitetura não suportada: $(uname -m)" >&2; exit 1 ;;
    esac
    echo "$so-$arq"
}

RID="${1:-$(rid_da_maquina)}"

case "$RID" in
    linux-x64)   ALVO=x86_64-unknown-linux-gnu;  ARQUIVO=libtypst_markdocs.so ;;
    linux-arm64) ALVO=aarch64-unknown-linux-gnu; ARQUIVO=libtypst_markdocs.so ;;
    osx-arm64)   ALVO=aarch64-apple-darwin;      ARQUIVO=libtypst_markdocs.dylib ;;
    osx-x64)     ALVO=x86_64-apple-darwin;       ARQUIVO=libtypst_markdocs.dylib ;;
    win-x64)     ALVO=x86_64-pc-windows-msvc;    ARQUIVO=typst_markdocs.dll ;;
    *) echo "RID não suportado: $RID" >&2; exit 1 ;;
esac

echo "▸ Compilando o crate em release para $ALVO ($RID)"
(cd "$CRATE" && cargo build --release --target "$ALVO")

SAIDA="$RAIZ/Artefatos/runtimes/$RID/native"
mkdir -p "$SAIDA"
cp "$CRATE/target/$ALVO/release/$ARQUIVO" "$SAIDA/$ARQUIVO"

echo "▸ Pronto: $SAIDA/$ARQUIVO ($(du -h "$SAIDA/$ARQUIVO" | cut -f1))"
echo "  Comite o artefato junto com o Cargo.lock quando for a fatia linux-x64."
