using System;

namespace Common.DAL.Shared.NoSql;

[AttributeUsage(validOn: AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class IdGenerationStrategyAttribute : Attribute
{
    public IdGenerationStrategyType Strategy { get; }

    public IdGenerationStrategyAttribute(IdGenerationStrategyType strategy)
    {
        Strategy = strategy;
    }
}
