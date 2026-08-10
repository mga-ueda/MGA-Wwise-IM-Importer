namespace MgaWwiseIMImporter.Domain;

/// <summary>リージョン端フェードのカーブ形状（Wwise MusicClip FadeIn/OutShape 相当）。</summary>
internal enum RegionFadeCurveKind
{
    LogarithmicBase3,
    SineConstantPowerFadeIn,
    LogarithmicBase141,
    InvertedSCurve,
    Linear,
    SCurve,
    ExponentialBase141,
    SineConstantPowerFadeOut,
    ExponentialBase3,
}
