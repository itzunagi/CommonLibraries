namespace Common.BusinessLogic.Models;

public abstract class ModelBase<TId>
{
    public TId Id { get; set; }
}
