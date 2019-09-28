using System;
using System.Collections.Generic;
using System.Text;

namespace Folke.CsTsService
{
    public class ReturnTypeAttribute : Attribute
    {
        public Type ReturnType { get; }

        public ReturnTypeAttribute(Type returnType)
        {
            ReturnType = returnType;
        }
    }
}
