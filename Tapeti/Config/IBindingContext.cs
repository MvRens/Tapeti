using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    public delegate object ValueFactory(IMessageContext context);
    public delegate Task ResultHandler(IMessageContext context, object value);


    public interface IBindingContext
    {
        Type MessageClass { get; set; }

        MethodInfo Method { get; }
        IReadOnlyList<IBindingParameter> Parameters { get; }
        IBindingResult Result { get; }

        void Use(IMessageFilterMiddleware filterMiddleware);
        void Use(IMessageMiddleware middleware);
    }


    public interface IBindingParameter
    {
        ParameterInfo Info { get; }
        bool HasBinding { get; }

        void SetBinding(ValueFactory valueFactory);
    }


    public interface IBindingResult
    {
        ParameterInfo Info { get; }
        bool HasHandler { get; }

        void SetHandler(ResultHandler resultHandler);
    }
}   
