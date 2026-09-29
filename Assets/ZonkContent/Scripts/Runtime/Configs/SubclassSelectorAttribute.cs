using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Для полей [SerializeReference]: в инспекторе появляется выпадающий список всех классов-наследников.
    /// Новое правило, модификатор, тактика ИИ или награда появляются в списке сами, как только написан класс.
    /// </summary>
    public sealed class SubclassSelectorAttribute : PropertyAttribute
    {
    }
}
