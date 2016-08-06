using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Folke.CsTsService.Nodes
{
    public class TypeNode
    {
        public TypeIdentifier Type { get; set; }
        public string Version { get; set; }
        public string Documentation { get; set; }
        public string Name { get; set; }
        public List<EnumValueNode> Values { get; set; }
        public List<PropertyNode> Properties { get; set; }

        public string CleanName
        {
            get
            {
                var name = Regex.Replace(Name, @"View(Model)?$", string.Empty);
                if (name == Name && IsObservable) return Name + "Data";
                return name;
            }
        }

        public bool IsReadOnly { get; set; }

        private bool? isObservable;

        public bool IsObservable
        {
            get
            {
                if (isObservable.HasValue) return isObservable.Value;
                isObservable = !IsReadOnly && Type == TypeIdentifier.Object && Properties.Any(x => x.IsObservable);
                return isObservable.Value;
            }
            set
            {
                isObservable = value;
            }
        }

        public TypeNode[] Union { get; internal set; }

        private bool? hasReadonly;

        public bool HasReadOnly()
        {
            if (!hasReadonly.HasValue)
            {
                hasReadonly = Type == TypeIdentifier.Object && Properties.Any(x => x.HasReadOnly());
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
                    property.Type.SetWritable();
            }
        }
    }
}
