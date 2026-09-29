using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.UI.Rules
{
    /// <summary>Страница правил: заголовок, текст и необязательная картинка.</summary>
    [Serializable]
    public sealed class RulesPage
    {
        public string TitleKey;
        public string TextKey;
        public Sprite Illustration;
    }

    /// <summary>
    /// Пример комбинации для списка слева: грани костей и название. Очки не пишутся руками, их считают правила
    /// (RuleSetConfig): если комбинацию выключить в правилах, строка пропадёт из книги сама.
    /// </summary>
    [Serializable]
    public sealed class ComboExample
    {
        public string NameKey;
        public int[] Faces = { 1 };
    }

    /// <summary>
    /// Книга правил: страницы справа и примеры комбинаций слева. Новая страница или пример — строка в ассете
    /// и ключи в Texts.csv, код не меняется.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/UI/Rules Book", fileName = "RulesBook")]
    public sealed class RulesBookConfig : ScriptableObject
    {
        [Tooltip("Правила, по которым считаются очки примеров")]
        public RuleSetConfig Rules;

        public List<ComboExample> Combos = new List<ComboExample>();
        public List<RulesPage> Pages = new List<RulesPage>();
    }
}
