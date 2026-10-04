namespace Domain.Common.Entities;

public interface IEntity<T> : IEntity
{
    public T Id { get; set; }
}

public interface IEntity
{
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}