using System;

namespace Common.DAL.Couchbase
{
    [AttributeUsage(validOn: AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class IdGenerationStrategyAttribute : Attribute
    {
        public IdGenerationStrategyType Strategy { get; }

        public IdGenerationStrategyAttribute(IdGenerationStrategyType strategy)
        {
            Strategy = strategy;
        }
    }

    public enum IdGenerationStrategyType
    {
        Auto,
        None
    }
}
