using System;
using System.Collections.Generic;
using System.Threading;
using Base.Services.Monetization;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Задания дня и недели: вкладки, время до обновления, строки заданий (копии шаблона QuestRowView).
    /// Выполненное задание забирается кнопкой; дневное невыполненное можно заменить за рекламу (GameConfig.QuestRerollsPerDay).
    /// Закрытие по кнопке «Назад» (WaitCloseRequestAsync).
    /// </summary>
    public sealed class QuestsWindow : UiWindow
    {
        private const string RerollPlacement = "quest_reroll";
        private const string WatchAdPlacement = "quest_watch_ad";

        [SerializeField] private TMP_Text _title;
        [SerializeField] private UiButtonView _dailyTab;
        [SerializeField] private UiButtonView _weeklyTab;
        [SerializeField] private TMP_Text _timer;
        [SerializeField] private RectTransform _list;
        [SerializeField] private QuestRowView _rowTemplate;
        [SerializeField] private UiButtonView _back;

        private IQuestService _quests;
        private IRewardService _rewards;
        private UiKit _kit;
        private QuestPeriod _period = QuestPeriod.Daily;
        private bool _busy;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, UiButtonView dailyTab, UiButtonView weeklyTab, TMP_Text timer, RectTransform list,
            QuestRowView rowTemplate, UiButtonView back)
        {
            _title = title;
            _dailyTab = dailyTab;
            _weeklyTab = weeklyTab;
            _timer = timer;
            _list = list;
            _rowTemplate = rowTemplate;
            _back = back;
        }
#endif

        [Inject]
        public void Construct(IQuestService quests, IRewardService rewards, UiKit kit)
        {
            _quests = quests;
            _rewards = rewards;
            _kit = kit;
        }

        private void Awake()
        {
            // Строка может быть префабом-деталью (Parts/QuestRow) или шаблоном внутри окна: выключаем только шаблон.
            if (_rowTemplate.gameObject.scene.IsValid())
                _rowTemplate.gameObject.SetActive(false);
            _dailyTab.OnClick(() => ShowPeriod(QuestPeriod.Daily));
            _weeklyTab.OnClick(() => ShowPeriod(QuestPeriod.Weekly));
            _back.OnClick(RequestClose);
        }

        protected override void OnShowing()
        {
            _title.text = T("quests.title");
            _dailyTab.SetText(T("quests.daily"));
            _weeklyTab.SetText(T("quests.weekly"));
            _back.SetText(T("ui.back"));
            ShowPeriod(QuestPeriod.Daily);
            TickTimerAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void ShowPeriod(QuestPeriod period)
        {
            _period = period;
            _dailyTab.SetColor(period == QuestPeriod.Daily ? UiColors.ButtonAccent : UiColors.ButtonMuted);
            _weeklyTab.SetColor(period == QuestPeriod.Weekly ? UiColors.ButtonAccent : UiColors.ButtonMuted);
            UpdateTimer();

            foreach (Transform child in _list)
            {
                if (child != _rowTemplate.transform)
                    Destroy(child.gameObject);
            }

            var slots = _quests.Get(period);
            foreach (var slot in slots)
            {
                var row = _rowTemplate.Spawn(_list);
                var target = slot.Target;
                row.Show(slot.Quest.Describe(T), slot.Progress, target,
                    T("quests.reward", RewardText(slot.Quest.Rewards)), slot.IsDone);

                var captured = slot;
                row.Claim.SetText(T(slot.IsClaimed ? "quests.claimed" : "quests.claim"));
                row.Claim.SetColor(slot.CanClaim ? UiColors.ButtonAccent : UiColors.ButtonMuted);
                row.Claim.Interactable = slot.CanClaim;
                row.Claim.OnClick(() => Claim(captured));

                // Задание «посмотреть рекламу»: пока не выполнено, кнопка сама показывает рекламу.
                if (!slot.IsDone && slot.Quest.Goal != null && slot.Quest.Goal.OffersAdButton)
                {
                    row.Claim.SetText(T("quests.watchAd"));
                    row.Claim.SetColor(UiColors.Button);
                    row.Claim.Interactable = _rewards.CanOffer;
                    row.Claim.OnClick(() => WatchAdAsync().Forget());
                }

                var canReroll = _quests.CanReroll(slot) && _rewards.CanOffer;
                row.Reroll.SetVisible(canReroll);
                row.Reroll.SetText(T("quests.reroll"));
                row.Reroll.OnClick(() => RerollAsync(captured).Forget());
            }
        }

        private void Claim(QuestSlot slot)
        {
            if (_busy)
                return;

            var granted = _quests.Claim(slot);
            if (granted.Count > 0)
            {
                var texts = new List<string>();
                foreach (var reward in granted)
                    texts.Add(reward.Currency != null ? reward.Amount + " " + T(reward.Currency.NameKey) : RewardNames.Describe(reward.Item, T));
                Toast.ShowAsync(_kit, transform, T("quests.got", string.Join(", ", texts)), UiColors.Good, 0.9f,
                    this.GetCancellationTokenOnDestroy()).Forget();
            }

            ShowPeriod(_period);
        }

        /// <summary>Реклама из окна заданий: прогресс засчитывает сам сервис заданий по событию награды.</summary>
        private async UniTaskVoid WatchAdAsync()
        {
            if (_busy)
                return;

            _busy = true;
            try
            {
                await _rewards.RequestAsync(WatchAdPlacement, this.GetCancellationTokenOnDestroy());
                if (this != null)
                    ShowPeriod(_period);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _busy = false;
            }
        }

        private async UniTaskVoid RerollAsync(QuestSlot slot)
        {
            if (_busy)
                return;

            _busy = true;
            try
            {
                var result = await _rewards.RequestAsync(RerollPlacement, this.GetCancellationTokenOnDestroy());
                if (this == null)
                    return;

                if (result.IsGranted())
                    _quests.Reroll(slot);
                ShowPeriod(_period);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _busy = false;
            }
        }

        private async UniTaskVoid TickTimerAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct).SuppressCancellationThrow())
                    return;
                UpdateTimer();
            }
        }

        private int _shownTimerMinutes = -1;
        private QuestPeriod _shownTimerPeriod;

        private void UpdateTimer()
        {
            var left = _quests.TimeToReset(_period);

            // Текст меняется раз в минуту: каждую секунду сравниваем число, а строку собираем только при смене.
            var minutes = (int)left.TotalMinutes;
            if (minutes == _shownTimerMinutes && _period == _shownTimerPeriod)
                return;

            _shownTimerMinutes = minutes;
            _shownTimerPeriod = _period;
            var time = left.TotalHours >= 24
                ? T("time.daysHours", (int)left.TotalDays, left.Hours)
                : T("time.hoursMinutes", (int)left.TotalHours, Math.Max(1, left.Minutes));
            _timer.text = T("quests.resetIn", time);
        }

        private string RewardText(IReadOnlyList<Reward> rewards)
        {
            var parts = new List<string>();
            foreach (var reward in rewards)
            {
                switch (reward)
                {
                    case CurrencyReward currency when currency.Currency != null:
                        parts.Add(currency.Amount + " " + T(currency.Currency.NameKey));
                        break;
                    case ContentReward content when content.Item != null:
                        parts.Add(RewardNames.Describe(content.Item, T));
                        break;
                }
            }

            return string.Join(", ", parts);
        }
    }
}
