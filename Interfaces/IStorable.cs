using System;
using Newtonsoft.Json.Linq;

namespace SNIBypassGUI.Interfaces
{
    // public interface IStorable<T> where T : IStorable<T>
#warning 在 .NET 7 及更高版本中可以考虑将 FromJObject 作为静态抽象成员。
    public interface IStorable
    {
        Guid Id { get; }

        JObject ToJObject();

        // static abstract T FromJObject(JObject obj);
    }
}
