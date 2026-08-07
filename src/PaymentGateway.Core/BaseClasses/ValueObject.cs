namespace PaymentGateway.Core.BaseClasses;

public abstract class ValueObject : IEquatable<ValueObject>
{
    // Força as classes filhas a escreverem uma definição para esse método
    protected abstract IEnumerable<object?> ObterComponentes();
    public bool Equals(ValueObject? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;

        return ObterComponentes().SequenceEqual(other.ObterComponentes());
    }

    public static bool operator ==(ValueObject esquerda, ValueObject direita) 
        => esquerda.Equals(direita);
    public static bool operator !=(ValueObject esquerda, ValueObject direita)
        => !(esquerda == direita);
}
