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

        public bool IsReadOnly { get; private set; } = true;

        private bool? hasObservable;

        public bool HasObservable
        {
            get
            {
                if (hasObservable.HasValue) return hasObservable.Value;
                hasObservable = !IsReadOnly && Properties != null && Properties.Any(x => x.Type.IsObservable);
                return hasObservable.Value;
            }
            set
            {
                hasObservable = value;
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
            if (Properties != null)
            {
                foreach (var property in Properties)
                {
                    if (property.Type.Type == TypeIdentifier.Object)
                        property.Type.Class.SetWritable();
                }
            }
        }
    }
}
