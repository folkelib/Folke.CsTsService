using System.Collections.Generic;
using System.Linq;

namespace Folke.CsTsService.Nodes
{
    public class ClassNode
    {
        public string Version { get; set; }
        public string Documentation { get; set; }
        public string KoName { get; set; }
        public List<PropertyNode> Properties { get; set; }
        public List<EnumValueNode> Values { get; set; }

        public List<string> GenericParameters { get; set; }

        public string Name { get; set; }

        public bool IsReadOnly { get; set; }

        private bool? isObservable;

        public bool IsObservable
        {
            get
            {
                if (isObservable.HasValue) return isObservable.Value;
                isObservable = !IsReadOnly && Properties != null && Properties.Any(x => x.Type.IsObservable);
                return isObservable.Value;
            }
            set
            {
                isObservable = value;
            }
        }

        private bool? hasReadonly;

        public bool HasReadOnly()
        {
            if (!hasReadonly.HasValue)
            {
                hasReadonly =  Properties != null && Properties.Any(x => x.HasReadOnly());
            }
            return hasReadonly.Value;
        }

        internal void SetWritable()
        {
            if (!IsReadOnly) return;

            IsReadOnly = false;
            foreach (var property in Properties)
            {
                if (property.Type.Type == TypeIdentifier.Object)
                    property.Type.Class.SetWritable();
            }
        }
    }
}
