using Yoga;

namespace NFMWorld.Mad.UI.yoga;

public readonly unsafe struct YGConfigPtr : IDisposable
{
    public readonly YGConfig* Ptr;

    public YGErrata Errata
    {
        get => YG.ConfigGetErrata(Ptr);
        set => YG.ConfigSetErrata(Ptr, value);
    }
    
    public float PointScaleFactor
    {
        get => YG.ConfigGetPointScaleFactor(Ptr);
        set => YG.ConfigSetPointScaleFactor(Ptr, value);
    }

    public void* Context
    {
        get => YG.ConfigGetContext(Ptr);
        set => YG.ConfigSetContext(Ptr, value);
    }

    public bool UseWebDefaults
    {
        get => YG.ConfigGetUseWebDefaults(Ptr) != 0;
        set => YG.ConfigSetUseWebDefaults(Ptr, value ? (byte)1 : (byte)0);
    }

    public YGConfigPtr()
    {
        Ptr = YG.ConfigNew();
    }

    internal YGConfigPtr(YGConfig* ptr)
    {
        Ptr = ptr;
    }

    public static YGConfigPtr GetDefault() => new(YG.ConfigGetDefault());
    
    public void SetExperimentalFeatureEnabled(YGExperimentalFeature feature, bool enabled)
    {
        YG.ConfigSetExperimentalFeatureEnabled(Ptr, feature, enabled ? (byte)1 : (byte)0);
    }
    
    public bool IsExperimentalFeatureEnabled(YGExperimentalFeature feature)
    {
        return YG.ConfigIsExperimentalFeatureEnabled(Ptr, feature) != 0;
    }
    
    public void SetLogger(delegate* unmanaged[Cdecl]<YGConfig*, YGNode*, YGLogLevel, sbyte*, sbyte*, int> logger)
    {
        YG.ConfigSetLogger(Ptr, logger);
    }
    
    public void SetCloneNodeFunc(delegate* unmanaged[Cdecl]<YGNode*, YGNode*, UIntPtr, YGNode*> cloneNodeFunc)
    {
        YG.ConfigSetCloneNodeFunc(Ptr, cloneNodeFunc);
    }

    public void Dispose()
    {
        YG.ConfigFree(Ptr);
    }
}