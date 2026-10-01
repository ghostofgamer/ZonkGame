using System;
using Base.Services.Quality;
using UnityEngine;
using Zenject;
using Zonk.Configs;

namespace Zonk.Presentation
{
    /// <summary>
    /// Выставляет стиль ToonStyleConfig глобально для шейдера Zonk/Toon: при запуске, при смене уровня качества
    /// и при смене локации (LightingDirector передаёт стиль профиля света). Работает только по событиям.
    /// </summary>
    public sealed class ToonStyleService : IInitializable, IDisposable
    {
        private static readonly int BlendId = Shader.PropertyToID("_ZonkToonBlend");
        private static readonly int ShadowTintId = Shader.PropertyToID("_ZonkToonShadowTint");
        private static readonly int MidTintId = Shader.PropertyToID("_ZonkToonMidTint");
        private static readonly int BandsId = Shader.PropertyToID("_ZonkToonBands");
        private static readonly int DotColorId = Shader.PropertyToID("_ZonkToonDotColor");
        private static readonly int DotsId = Shader.PropertyToID("_ZonkToonDots");
        private static readonly int RimColorId = Shader.PropertyToID("_ZonkToonRimColor");
        private static readonly int RimId = Shader.PropertyToID("_ZonkToonRim");
        private static readonly int SpecId = Shader.PropertyToID("_ZonkToonSpec");
        private static readonly int OutlineColorId = Shader.PropertyToID("_ZonkToonOutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_ZonkToonOutlineWidth");

        private readonly GameConfig _config;
        private readonly IQualityService _quality;
        private ToonStyleConfig _locationStyle;

        public ToonStyleService(GameConfig config, IQualityService quality)
        {
            _config = config;
            _quality = quality;
        }

        /// <summary>Стиль, который действует сейчас.</summary>
        public ToonStyleConfig Current => _locationStyle != null ? _locationStyle : _config.ToonStyle;

        public void Initialize()
        {
            _quality.TierChanged += Refresh;
#if UNITY_EDITOR
            ToonStyleConfig.EditorChanged += OnEditorChanged;
#endif
            Refresh();
        }

        public void Dispose()
        {
            _quality.TierChanged -= Refresh;
#if UNITY_EDITOR
            ToonStyleConfig.EditorChanged -= OnEditorChanged;
#endif
        }

        /// <summary>Стиль локации; null — общий стиль игры.</summary>
        public void SetLocationStyle(ToonStyleConfig style)
        {
            if (style == _locationStyle)
                return;

            _locationStyle = style;
            Refresh();
        }

        public void Refresh()
        {
            Apply(Current, _quality.Tier);
        }

#if UNITY_EDITOR
        private void OnEditorChanged(ToonStyleConfig style)
        {
            if (style == Current)
                Refresh();
        }
#endif

        /// <summary>Выставить стиль глобально. Без стиля — обычный плавный свет (Blend = 0).</summary>
        public static void Apply(ToonStyleConfig style, QualityTier tier)
        {
            if (style == null)
            {
                Shader.SetGlobalFloat(BlendId, 0f);
                Shader.SetGlobalFloat(OutlineWidthId, 0f);
                return;
            }

            Shader.SetGlobalFloat(BlendId, style.Blend);
            Shader.SetGlobalColor(ShadowTintId, style.ShadowTint);
            Shader.SetGlobalColor(MidTintId, style.MidTint);
            Shader.SetGlobalVector(BandsId, new Vector4(style.ShadowThreshold, style.LightThreshold, style.Softness, style.DotStart));
            Shader.SetGlobalColor(DotColorId, style.DotColor);
            var dots = tier >= style.DotsFrom ? style.DotStrength : 0f;
            Shader.SetGlobalVector(DotsId, new Vector4(style.DotCell, style.DotMaxRadius, dots, style.DotAngle * Mathf.Deg2Rad));
            Shader.SetGlobalColor(RimColorId, style.RimColor);
            Shader.SetGlobalVector(RimId, new Vector4(style.RimThreshold, style.RimStrength, style.RimInShadow, 0f));
            Shader.SetGlobalVector(SpecId, new Vector4(style.SpecularThreshold, style.SpecularStrength, 0f, 0f));
            Shader.SetGlobalColor(OutlineColorId, style.OutlineColor);
            Shader.SetGlobalFloat(OutlineWidthId, tier >= style.OutlineFrom ? style.OutlineWidth : 0f);
        }
    }
}
