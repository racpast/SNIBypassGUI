using System;
using Newtonsoft.Json.Linq;

namespace SNIBypassGUI.Interfaces
{
    // public interface IStorable<T> where T : IStorable<T>
    [Obsolete("在升级到 .NET 7 或更高版本时，可以考虑将 FromJObject 作为静态抽象成员。")]
    public interface IStorable
    {
        Guid Id { get; }

        JObject ToJObject();

        // static abstract T FromJObject(JObject obj);
    }
}
