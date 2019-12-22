namespace Mogu
{
    public readonly struct InjectorOption
    {
        public static InjectorOption Default = new InjectorOption(-1);

        public readonly int InjectDllTimeOut;

        public InjectorOption(int injectDllTimeOut)  
        {
            this.InjectDllTimeOut = injectDllTimeOut;
        }
    }
}
