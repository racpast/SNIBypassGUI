namespace SNIBypassGUI.Interfaces
{
    public interface IFactory<T>
    {
        /// <summary>
        /// 创建一个新的 <typeparamref name="T"/> 实例。
        /// </summary>
        public T CreateDefault();
    }
}
