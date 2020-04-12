using System;
using System.Text.Json.Serialization;

namespace Folke.CsTsService.Optional
{

    [JsonConverter(typeof(OptionalJsonConverterFactory))]
    public struct Optional<T>
    {
        public bool IsSet { get; private set; }

        private T _value;

        public static Optional<T> Undefined => default;

        public Optional(T value)
        {
            IsSet = true;
            _value = value;
        }

        public T Value
        {
            get
            {
                if (IsSet) return _value;

                throw new Exception("Value is not set");
            }
            set
            {
                IsSet = true;
                _value = value;
            }
        }

        public static implicit operator Optional<T>(T value)
        {
            return new Optional<T>
            {
                IsSet = true,
                _value = value,
            };
        }

        public static implicit operator T(Optional<T> value)
        {
            return value._value;
        }

        public T GetValueOrDefault()
        {
            return _value;
        }

        public T GetValueOrDefault(T defaultValue)
        {
            if (!IsSet) return defaultValue;

            return _value;
        }

        public override bool Equals(object other)
        {
            if (!IsSet) return other == null;
            if (other == null) return false;
            return _value == null ? other == null : _value.Equals(other);
        }

        public static bool operator ==(Optional<T> t1, Optional<T> t2)
        {
            // undefined equals undefined
            if (!t1.IsSet && !t2.IsSet) return true;

            // undefined != everything else
            if (t1.IsSet ^ t2.IsSet) return false;

            // if both are values, compare them
            return t1._value == null ? t2._value == null : t1._value.Equals(t2._value);
        }

        public static bool operator !=(Optional<T> t1, Optional<T> t2)
        {
            return !(t1 == t2);
        }

        public override int GetHashCode()
        {
            if (!IsSet || _value == null) return -1;
            return _value.GetHashCode();
        }

        public override string ToString()
        {
            return IsSet ? _value == null ? "null" : _value.ToString() : "undefined";
        }
    }
}