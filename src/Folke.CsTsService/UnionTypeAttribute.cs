using System;

namespace Folke.CsTsService
{
    public class UnionTypeAttribute : Attribute
    {
        public Type Type { get; set; }
        public UnionTypeAttribute(Type type)
        {
            Type = type;
        }
    }
}
