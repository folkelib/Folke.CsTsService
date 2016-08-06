using System;

namespace Folke.CsTsService.Nodes
{
    public class PropertyNode : IConstraintsNode, ITypedNode
    {
        public string Name { get; set; }
        public bool IsRequired { get; set; }
        public int? MinimumLength { get; set; }
        public int? MaximumLength { get; set; }
        public Format Format { get; set; }
        public string CompareTo { get; set; }
        public object Minimum { get; set; }
        public object Maximum { get; set; }
        public TypeNode Type { get; set; }
        public bool IsCollection { get; set; }
        public bool IsDictionary { get; set; }
        public string Documentation { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsObservable { get; set; }

        public bool NeedValidation()
        {
            return IsObservable && (IsRequired || MinimumLength.HasValue || MaximumLength.HasValue || CompareTo != null ||
                   Minimum != null || Maximum != null || Format != Format.None);
        }

        private bool? hasReadonly;
        internal bool HasReadOnly()
        {
            if (IsReadOnly) return IsReadOnly;
            if (!hasReadonly.HasValue)
            {
                hasReadonly = false;
                hasReadonly = Type.Type == TypeIdentifier.Object && Type.HasReadOnly();
            }
            return hasReadonly.Value;
        }
    }
}
