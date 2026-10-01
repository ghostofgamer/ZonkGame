using System.Collections.Generic;
using System.Linq;
using Base.Core.Localization;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Editor
{
    /// <summary>
    /// Проверка контента: уникальные ID, пустые ссылки, ключи локализации, веса костей, соглашение граней
    /// у мешей костей, горло "Mouth" у стаканов. Меню Zonk/Content/Validate и автоматически перед сборкой:
    /// сломанный контент в билд не попадает.
    /// </summary>
    public sealed class ContentValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 10;

        public void OnPreprocessBuild(BuildReport report)
        {
            var errors = Validate(out _);
            if (errors.Count > 0)
                throw new BuildFailedException("[Content] " + errors.Count + " content errors, see Zonk/Content/Validate:\n" +
                                               string.Join("\n", errors.Take(20)));
        }

        [MenuItem("Zonk/Content/Validate", priority = 11)]
        public static void ValidateMenu()
        {
            var errors = Validate(out var warnings);
            foreach (var warning in warnings)
                Debug.LogWarning("[Content] " + warning);
            foreach (var error in errors)
                Debug.LogError("[Content] " + error);

            Debug.Log($"[Content] Validation finished: {errors.Count} errors, {warnings.Count} warnings");
        }

        public static List<string> Validate(out List<string> warnings)
        {
            var errors = new List<string>();
            warnings = new List<string>();

            ContentDatabaseBuilder.Rebuild();
            var database = ContentDatabaseBuilder.FindDatabase();
            if (database == null)
            {
                errors.Add("ContentDatabase not found");
                return errors;
            }

            var texts = LoadTexts(warnings);
            var config = AssetDatabase.FindAssets("t:GameConfig").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameConfig>).FirstOrDefault(c => c != null);
            var ids = new Dictionary<string, ContentConfig>();

            foreach (var item in database.Items)
            {
                if (item == null)
                    continue;

                if (string.IsNullOrWhiteSpace(item.Id))
                    errors.Add($"{item.name}: empty Id");
                else if (ids.TryGetValue(item.Id, out var other))
                    errors.Add($"{item.name}: Id '{item.Id}' is also used by {other.name}");
                else
                    ids[item.Id] = item;

                CheckKey(item, item.NameKey, texts, errors);

                switch (item)
                {
                    case DieConfig die:
                        ValidateDie(die, texts, errors);
                        if (die.IsSpecial && die.Price.IsFree)
                            warnings.Add($"{die.name}: special die without price — free for everyone");
                        break;
                    case CosmeticSlotConfig slot:
                        if (slot.DefaultItem == null)
                            errors.Add($"{slot.name}: DefaultItem is not set");
                        if (string.IsNullOrEmpty(slot.CameraShotId))
                            warnings.Add($"{slot.name}: CameraShotId is empty");
                        break;
                    case CosmeticItemConfig cosmetic:
                        ValidateCosmetic(cosmetic, errors, warnings);
                        // Предмет без цены есть у всех: так задуман только базовый предмет слота (и запасной скин второго игрока).
                        if (cosmetic.Price.IsFree && cosmetic.Slot != null && cosmetic.Slot.DefaultItem != cosmetic && !cosmetic.HiddenInShop &&
                            (config == null || config.SecondPlayerFallbackSkin != cosmetic))
                            warnings.Add($"{cosmetic.name}: no price — free for everyone. Tick a way to get it (coins, ads, money)");
                        ValidatePrice(cosmetic, cosmetic.Price, errors);
                        break;
                    case ThemeSetConfig theme:
                        if (theme.Items.Any(i => i == null))
                            errors.Add($"{theme.name}: empty item in the set");
                        ValidatePrice(theme, theme.Price, errors);
                        break;
                    case OpponentConfig opponent:
                        ValidateOpponent(opponent, texts, errors, warnings, config);
                        if (opponent.IsDread && !database.All<ChapterConfig>().Any(c => c.DreadBosses.Contains(opponent)))
                            warnings.Add($"{opponent.name}: dread version is not in any ChapterConfig.DreadBosses — players never see it");
                        break;
                    case LeaderboardConfig board:
                        if (string.IsNullOrEmpty(board.TechnicalName))
                            errors.Add($"{board.name}: TechnicalName (name of the table on the platform) is empty");
                        break;
                    case CoinPackConfig pack:
                        if (string.IsNullOrEmpty(pack.ProductId) || pack.Currency == null || pack.Amount <= 0)
                            errors.Add($"{pack.name}: coin pack needs ProductId, currency and amount");
                        break;
                    case QuestConfig quest:
                        if (quest.Goal == null)
                            errors.Add($"{quest.name}: quest goal is not set");
                        else if (quest.Goal is WinWithDieGoal withDie && withDie.Die == null)
                            errors.Add($"{quest.name}: die for the quest is not set");
                        else if (quest.Goal is CustomEventGoal custom && string.IsNullOrEmpty(custom.Tag))
                            errors.Add($"{quest.name}: custom event tag is empty");
                        else if (!string.IsNullOrEmpty(quest.Goal.TextArgKey))
                            CheckKey(quest, quest.Goal.TextArgKey, texts, errors);
                        if (quest.Rewards.Count == 0 || quest.Rewards.Any(r => r == null))
                            errors.Add($"{quest.name}: quest has no reward or an empty reward");
                        if (quest.Conditions.Any(c => c == null))
                            errors.Add($"{quest.name}: empty condition");
                        break;
                    case ChapterConfig chapter:
                        if (chapter.Opponents.Count == 0 || chapter.Opponents.Any(o => o == null))
                            errors.Add($"{chapter.name}: opponents list is empty or has empty entries");
                        foreach (var line in chapter.Intro.Concat(chapter.Outro))
                        {
                            CheckKey(chapter, line.SpeakerKey, texts, errors);
                            CheckKey(chapter, line.TextKey, texts, errors);
                        }

                        // Грозные версии: ссылаются на босса этой же главы, сами не в списке соперников.
                        foreach (var dread in chapter.DreadBosses)
                        {
                            if (dread == null)
                                errors.Add($"{chapter.name}: empty entry in DreadBosses");
                            else if (dread.DreadOf == null)
                                errors.Add($"{chapter.name}: {dread.name} is in DreadBosses but DreadOf is not set");
                            else if (!chapter.Opponents.Contains(dread.DreadOf) || !dread.DreadOf.IsBoss)
                                errors.Add($"{chapter.name}: {dread.name}.DreadOf must be a boss of this chapter");
                            else if (chapter.Opponents.Contains(dread))
                                errors.Add($"{chapter.name}: {dread.name} must be only in DreadBosses, not in Opponents");
                            else if (!dread.IsBoss)
                                warnings.Add($"{dread.name}: dread version should be marked IsBoss (quests, reactions, dice rules)");
                        }

                        break;
                    case ReactionSetConfig reactions:
                        foreach (var entry in reactions.Entries)
                        foreach (var key in entry.LineKeys)
                            CheckKey(reactions, key, texts, errors);
                        break;
                    case GameModeConfig mode:
                        if (mode.Rules == null)
                            errors.Add($"{mode.name}: Rules are not set");
                        break;
                    case RuleSetConfig rules:
                        if (rules.Rules == null || rules.Rules.Count == 0 || rules.Rules.Any(r => r == null))
                            errors.Add($"{rules.name}: scoring rules are empty");
                        break;
                }
            }

            ValidateGameConfig(errors);
            ValidateModes(texts, errors, warnings);
            ValidateShopVariety(database, warnings);
            ValidateScriptFiles(errors);
            return errors;
        }

        /// <summary>
        /// Unity связывает ScriptableObject и MonoBehaviour с файлом того же имени. Класс в чужом файле
        /// даёт ассеты без скрипта: поля не сохраняются, ссылки пустые.
        /// </summary>
        private static void ValidateScriptFiles(List<string> errors)
        {
            var scripted = new HashSet<System.Type>(MonoImporter.GetAllRuntimeMonoScripts()
                .Select(script => script.GetClass())
                .Where(type => type != null));

            var types = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Concat(TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
                .Where(type => !type.IsAbstract && type.Assembly.GetName().Name == "Zonk");

            foreach (var type in types)
            {
                if (!scripted.Contains(type))
                    errors.Add($"{type.Name}: class must be in its own file {type.Name}.cs");
            }
        }

        private static void ValidateDie(DieConfig die, HashSet<string> texts, List<string> errors)
        {
            if (die.Weights == null || die.Weights.Length != 6 || die.Weights.Any(w => w < 0) || die.Weights.Sum() <= 0)
                errors.Add($"{die.name}: needs 6 non-negative weights");
            if (die.IsSpecial)
                CheckKey(die, die.DescriptionKey, texts, errors);
            ValidatePrice(die, die.Price, errors);
        }

        private static void ValidateCosmetic(CosmeticItemConfig item, List<string> errors, List<string> warnings)
        {
            if (item.Slot == null)
            {
                errors.Add($"{item.name}: Slot is not set");
                return;
            }

            switch (item.Payload)
            {
                case null:
                    errors.Add($"{item.name}: Payload is not set");
                    break;
                case PrefabPayload prefab when Setup.ZonkSetup.EditorPrefabOf(prefab) == null:
                    errors.Add($"{item.name}: prefab is not set");
                    break;
                case PrefabPayload prefab when item.Slot.Id == "cup" &&
                                               FindDeep(Setup.ZonkSetup.EditorPrefabOf(prefab).transform, CupView.MouthName) == null:
                    errors.Add($"{item.name}: cup prefab has no child '{CupView.MouthName}'");
                    break;
                case PrefabPayload prefab when prefab.Prefab != null:
                    warnings.Add($"{item.name}: prefab is a direct reference (loads at startup with all content); " +
                                 "run Zonk/Setup/Build Everything to move it to on-demand loading");
                    break;
                case MeshMaterialPayload meshMaterial:
                    if (meshMaterial.Material == null)
                        errors.Add($"{item.name}: material is not set");
                    if (meshMaterial.Mesh != null && item.Slot.Applier is DiceSkinApplier)
                        ValidateDieMesh(item.name, meshMaterial.Mesh, errors);
                    if (meshMaterial.Material != null && item.Slot.Applier is DiceSkinApplier && !HasEmission(meshMaterial.Material))
                        warnings.Add($"{item.name}: material has no emission, special dice markers will not glow");
                    break;
                case RollStylePayload rollStyle when rollStyle.Style == null:
                    errors.Add($"{item.name}: roll style is not set");
                    break;
                case MaterialPayload material when material.Material == null:
                    errors.Add($"{item.name}: material is not set");
                    break;
            }
        }

        /// <summary>
        /// Меш кости должен соблюдать соглашение DieFaces: у плоской грани с нормалью по оси UV лежат в ячейке
        /// атласа с тем же значением (3×2, сверху 1 2 3, снизу 4 5 6).
        /// </summary>
        public static void ValidateDieMesh(string owner, Mesh mesh, List<string> errors)
        {
            if (!mesh.isReadable)
            {
                errors.Add($"{owner}: die mesh '{mesh.name}' must have Read/Write enabled for validation");
                return;
            }

            var normals = mesh.normals;
            var uvs = mesh.uv;
            if (uvs.Length == 0)
            {
                errors.Add($"{owner}: die mesh '{mesh.name}' has no UV");
                return;
            }

            for (var face = 1; face <= 6; face++)
            {
                var axis = DieFaces.Normal(face);
                var found = false;
                for (var i = 0; i < normals.Length; i++)
                {
                    if (Vector3.Dot(normals[i], axis) < 0.999f)
                        continue;

                    found = true;
                    var column = Mathf.Clamp((int)(uvs[i].x * 3f), 0, 2);
                    var row = uvs[i].y >= 0.5f ? 0 : 1;
                    var value = row * 3 + column + 1;
                    if (value != face)
                    {
                        errors.Add($"{owner}: die mesh '{mesh.name}' face {face} ({axis}) uses atlas cell {value}");
                        break;
                    }
                }

                if (!found)
                    errors.Add($"{owner}: die mesh '{mesh.name}' has no flat face for {face} ({axis})");
            }
        }

        private static void ValidateOpponent(OpponentConfig opponent, HashSet<string> texts, List<string> errors,
            List<string> warnings, GameConfig config)
        {
            // Особые кости: у обычных соперников немного, у боссов больше; одна особая кость — один слот.
            var special = opponent.Dice.Where(d => d != null && d.IsSpecial).ToList();
            if (special.Count != special.Distinct().Count())
                errors.Add($"{opponent.name}: the same special die is used twice (one special die = one slot)");
            if (special.Count > 6)
                errors.Add($"{opponent.name}: more than 6 dice");
            if (config != null && !opponent.IsBoss && special.Count > config.OpponentMaxSpecialDice)
                warnings.Add($"{opponent.name}: {special.Count} special dice, regular opponents have up to {config.OpponentMaxSpecialDice}");
            if (config != null && opponent.IsBoss && special.Count < config.BossMinSpecialDice)
                warnings.Add($"{opponent.name}: boss has {special.Count} special dice, bosses have at least {config.BossMinSpecialDice}");

            if (opponent.Ai == null)
                errors.Add($"{opponent.name}: AI profile is not set");
            CheckKey(opponent, opponent.TitleKey, texts, errors);
            if (!string.IsNullOrEmpty(opponent.RuleKey))
                CheckKey(opponent, opponent.RuleKey, texts, errors);
            if (opponent.Modifiers.Any(m => m == null))
                errors.Add($"{opponent.name}: empty modifier");
            // Правила показываются игроку из самих правил: у каждого — описание и все его тексты в Texts.csv.
            foreach (var modifier in opponent.Modifiers.Where(m => m != null))
                CheckModifierTexts(opponent, modifier, texts, errors);
            if (opponent.Modifiers.Any(m => m != null && string.IsNullOrEmpty(m.DescriptionKey)) && string.IsNullOrEmpty(opponent.RuleKey))
                errors.Add($"{opponent.name}: a rule has no description (MatchModifier.DescriptionKey) and there is no RuleKey");
            if (opponent.FirstWinRewards.Concat(opponent.RepeatWinRewards).Any(r => r == null))
                errors.Add($"{opponent.name}: empty reward");
            if (opponent.StarConditions.Any(c => c == null))
                errors.Add($"{opponent.name}: empty star condition");
            foreach (var condition in opponent.StarConditions.Where(c => c != null))
                CheckKey(opponent, condition.TextKey, texts, errors);
        }

        private static void ValidatePrice(ContentConfig owner, Price price, List<string> errors)
        {
            if (price == null)
                return;

            foreach (var option in price.Options)
            {
                switch (option)
                {
                    case null:
                        errors.Add($"{owner.name}: empty price option");
                        break;
                    case CurrencyPriceOption currency when currency.Currency == null || currency.Amount <= 0:
                        errors.Add($"{owner.name}: currency price without currency or amount");
                        break;
                    case PurchasePriceOption purchase when string.IsNullOrWhiteSpace(purchase.ProductId):
                        errors.Add($"{owner.name}: purchase price without ProductId");
                        break;
                }
            }
        }

        /// <summary>
        /// Правило магазина: в каждой вкладке (слот косметики, особые кости) есть что получить за монеты, за рекламу
        /// и за покупку. Нарушение — предупреждение: вкладку можно временно оставить без одного вида.
        /// </summary>
        private static void ValidateShopVariety(ContentDatabase database, List<string> warnings)
        {
            var tabs = new Dictionary<string, List<Price>>();
            foreach (var item in database.All<CosmeticItemConfig>())
            {
                if (item.Slot == null || !item.Slot.ShowInShop || item.HiddenInShop)
                    continue;
                if (!tabs.TryGetValue(item.Slot.name, out var prices))
                    tabs[item.Slot.name] = prices = new List<Price>();
                prices.Add(item.Price);
            }

            tabs["Dice"] = database.All<DieConfig>().Where(d => d.IsSpecial).Select(d => d.Price).ToList();

            foreach (var tab in tabs)
            {
                var options = tab.Value.Where(p => p != null).SelectMany(p => p.Options).ToList();
                if (!options.Any(o => o is CurrencyPriceOption))
                    warnings.Add($"Shop tab {tab.Key}: nothing for coins");
                if (!options.Any(o => o is RewardedAdPriceOption))
                    warnings.Add($"Shop tab {tab.Key}: nothing for rewarded ads");
                if (!options.Any(o => o is PurchasePriceOption))
                    warnings.Add($"Shop tab {tab.Key}: nothing for real money");
            }
        }

        /// <summary>Режимы-испытания: соперники, стражи, этажи башни, правила с текстами.</summary>
        private static void ValidateModes(HashSet<string> texts, List<string> errors, List<string> warnings)
        {
            var guid = AssetDatabase.FindAssets("t:" + nameof(GameConfig), new[] { ContentDatabaseBuilder.GameFolder }).FirstOrDefault();
            var config = guid != null ? AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guid)) : null;
            if (config == null)
                return;

            var run = config.EndlessRun;
            if (run == null)
            {
                warnings.Add("GameConfig: EndlessRun is not set — the mode says «not ready»");
            }
            else
            {
                if (run.Opponents.Count == 0 || run.Opponents.Any(o => o == null))
                    errors.Add($"{run.name}: Opponents list is empty or has empty entries");
                if (run.Guardians.Any(o => o == null))
                    errors.Add($"{run.name}: empty guardian");
                if (run.AiEarly == null || run.AiMid == null || run.AiLate == null)
                    warnings.Add($"{run.name}: AI profile for a stage is not set — the opponent's own AI is used");
                if (run.RulePool.Any(r => r == null))
                    errors.Add($"{run.name}: empty rule in RulePool");
                foreach (var rule in run.RulePool.Where(r => r != null))
                    CheckModifierTexts(run, rule, texts, errors);
                if (run.Milestones.Any(m => m == null || m.Rewards.Any(r => r == null)))
                    errors.Add($"{run.name}: empty milestone or reward");
            }

            var tower = config.Tower;
            if (tower == null)
            {
                warnings.Add("GameConfig: Tower is not set — the mode says «not ready»");
                return;
            }

            for (var i = 0; i < tower.Floors.Count; i++)
            {
                var floor = tower.Floors[i];
                if (floor == null || floor.Opponent == null)
                {
                    errors.Add($"{tower.name}: floor {i + 1} has no opponent");
                    continue;
                }

                if (floor.Target <= 0)
                    errors.Add($"{tower.name}: floor {i + 1} target must be positive");
                if (floor.Rules.Any(r => r == null) || floor.FirstClearRewards.Any(r => r == null))
                    errors.Add($"{tower.name}: floor {i + 1} has an empty rule or reward");
                foreach (var rule in floor.Rules.Where(r => r != null))
                    CheckModifierTexts(tower, rule, texts, errors);
            }
        }

        private static void ValidateGameConfig(List<string> errors)
        {
            var guid = AssetDatabase.FindAssets("t:" + nameof(GameConfig), new[] { ContentDatabaseBuilder.GameFolder }).FirstOrDefault();
            var config = guid != null ? AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guid)) : null;
            if (config == null)
            {
                errors.Add("GameConfig not found");
                return;
            }

            if (config.StandardDie == null) errors.Add("GameConfig: StandardDie is not set");
            if (config.Coins == null) errors.Add("GameConfig: Coins is not set");
            if (config.Energy == null) errors.Add("GameConfig: Energy is not set");
            if (config.HotSeatMode == null) errors.Add("GameConfig: HotSeatMode is not set");
            if (config.CampaignMode == null) errors.Add("GameConfig: CampaignMode is not set");

            // Уровни мастерства по возрастанию очков, у каждого название.
            for (var i = 0; i < config.MasteryLevels.Count; i++)
            {
                var level = config.MasteryLevels[i];
                if (level == null || string.IsNullOrEmpty(level.NameKey))
                    errors.Add($"GameConfig: mastery level {i + 1} has no NameKey");
                else if (i > 0 && config.MasteryLevels[i - 1] != null && level.Points <= config.MasteryLevels[i - 1].Points)
                    errors.Add($"GameConfig: mastery level {i + 1} needs more points than level {i}");
                if (level != null && level.Rewards.Any(r => r == null))
                    errors.Add($"GameConfig: mastery level {i + 1} has an empty reward");
            }
        }

        private static HashSet<string> LoadTexts(List<string> warnings)
        {
            var keys = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { ContentDatabaseBuilder.GameFolder + "/Localization" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null)
                    continue;

                var table = CsvLocalizationParser.Parse(asset.text, out var missing);
                foreach (var key in table.Keys)
                    keys.Add(key);
                foreach (var key in missing)
                    warnings.Add($"{asset.name}: no translation for '{key}'");
            }

            return keys;
        }

        /// <summary>Текст правила и ключи в его значениях («@…»: название комбинации, грани) есть в Texts.csv.</summary>
        private static void CheckModifierTexts(Object owner, Core.Modifiers.MatchModifier modifier, HashSet<string> texts,
            List<string> errors)
        {
            if (string.IsNullOrEmpty(modifier.DescriptionKey))
                return;

            CheckKey(owner, modifier.DescriptionKey, texts, errors);
            foreach (var arg in modifier.DescriptionArgs ?? System.Array.Empty<object>())
            {
                if (arg is string text && text.StartsWith("@"))
                    CheckKey(owner, text.Substring(1), texts, errors);
            }
        }

        private static void CheckKey(Object owner, string key, HashSet<string> texts, List<string> errors)
        {
            if (string.IsNullOrEmpty(key))
            {
                errors.Add($"{owner.name}: localization key is empty");
                return;
            }

            if (texts.Count > 0 && !texts.Contains(key))
                errors.Add($"{owner.name}: key '{key}' is missing in localization CSV");
        }

        /// <summary>
        /// Светится ли материал (метки особых костей задают _EmissionColor). URP Lit включает свечение ключом
        /// _EMISSION; шейдер без такого ключа (Zonk/Toon) добавляет свечение всегда.
        /// </summary>
        private static bool HasEmission(Material material)
        {
            if (!material.HasProperty("_EmissionColor"))
                return false;

            var keyword = material.shader.keywordSpace.FindKeyword("_EMISSION");
            return !keyword.isValid || material.IsKeywordEnabled(keyword);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
