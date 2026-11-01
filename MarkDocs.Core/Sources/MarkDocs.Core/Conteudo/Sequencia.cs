using System.Collections;
using System.Runtime.CompilerServices;

namespace MarkDocs.Core.Conteudo;

/// <summary>
/// Uma lista imutável com <b>igualdade de valor</b>. Existe para que os
/// <c>record</c>s da árvore se comparem como os <c>enum</c>s <c>Equatable</c> do
/// Swift: dois <see cref="BlocoDoc"/> são iguais quando têm os mesmos filhos,
/// e não quando apontam para a mesma lista.
/// </summary>
[CollectionBuilder(typeof(Sequencia), nameof(Sequencia.Criar))]
internal sealed class Sequencia<T> : IReadOnlyList<T>, IEquatable<Sequencia<T>>
{
    private readonly T[] _itens;

    public Sequencia(IEnumerable<T> itens) => _itens = itens.ToArray();

    public static readonly Sequencia<T> Vazia = new([]);

    public T this[int indice] => _itens[indice];
    public int Count => _itens.Length;

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_itens).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _itens.GetEnumerator();

    public bool Equals(Sequencia<T>? outra) => outra is not null && _itens.SequenceEqual(outra._itens);
    public override bool Equals(object? obj) => obj is Sequencia<T> outra && Equals(outra);
    public override int GetHashCode() => _itens.Aggregate(17, (h, x) => HashCode.Combine(h, x));
    public override string ToString() => "[" + string.Join(", ", _itens) + "]";
}

internal static class Sequencia
{
    public static Sequencia<T> Criar<T>(ReadOnlySpan<T> itens) => new(itens.ToArray());

    public static Sequencia<T> ParaSequencia<T>(this IEnumerable<T> itens) => new(itens);
}
