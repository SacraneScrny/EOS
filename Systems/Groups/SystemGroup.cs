namespace EOS.Systems.Groups
{
    public abstract class SystemGroup
    {
        public void Enable() => SystemGroups.SetEnabled(GetType(), true);
        public void Disable() => SystemGroups.SetEnabled(GetType(), false);
        public bool IsEnabled => SystemGroups.IsEnabled(GetType());
    }
}