using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>Ракурс камеры: точка и угол обзора. ID используют состояния стола и слоты магазина.</summary>
    public sealed class CameraShot : MonoBehaviour
    {
        [SerializeField] private string _id;
        [SerializeField] private float _fieldOfView = 45f;

        public string Id => _id;
        public float FieldOfView => _fieldOfView;

#if UNITY_EDITOR
        public void EditorSetup(string id, float fieldOfView)
        {
            _id = id;
            _fieldOfView = fieldOfView;
        }
#endif
    }

    /// <summary>Известные ракурсы. Ракурсы магазина задаются в слотах косметики (CameraShotId).</summary>
    public static class CameraShots
    {
        public const string Menu = "menu";
        public const string Match = "match";
        public const string Top = "top";
        public const string Opponent = "opponent";
    }
}
