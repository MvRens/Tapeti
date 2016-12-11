using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tapeti.Config
{
    public delegate object ValueFactory(IMessageContext context);


    public interface IBindingContext
    {
        Type MessageClass { get; set; }
        IReadOnlyList<IBindingParameter> Parameters { get; }
    }


    public interface IBindingParameter
    {
        ParameterInfo Info { get; }
        bool HasBinding { get; }

        void SetBinding(ValueFactory valueFactory);
    }
}   
