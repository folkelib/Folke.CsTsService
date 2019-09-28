using System;

namespace Folke.CsTsService.Nodes
{
    public class PropertyNode : IConstraintsNode
    {
        public string Name { get; set; }
        public bool IsRequired { get; set; }
        public int? MinimumLength { get; set; }
        public int? MaximumLength { get; set; }
        public Format Format { get; set; }
        public string? CompareTo { get; set; }
        public object? Minimum { get; set; }
        public object? Maximum { get; set; }
        public TypeNode Type { get; set; }
        public string? Documentation { get; set; }
        public bool IsReadOnly { get; set; }
        
        private bool? hasReadonly;
        internal bool HasReadOnly()
        {
            if (IsReadOnly) return IsReadOnly;
            if (!hasReadonly.HasValue)
            {
                hasReadonly = false;
                hasReadonly = Type.Type == TypeIdentifier.Object && Type.Class != null && Type.Class.HasReadOnly();
            }
            return hasReadonly.Value;
        }

        public PropertyNode(string name, TypeNode type)
        {
            Name = name;
            Type = type;
        }
    }
}
