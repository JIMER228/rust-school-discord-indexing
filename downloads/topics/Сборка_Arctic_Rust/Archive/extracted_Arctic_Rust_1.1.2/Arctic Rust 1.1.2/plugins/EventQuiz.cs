using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ConVar;
namespace Oxide.Plugins
{
    [Info("EventQuiz", "RustPlugin.ru", "1.0.31")]
    class EventQuiz : RustPlugin
    {
        private PluginConfig _config;
        private Timer _timerAutoStart;
        private EventData _eventData;
        private List<Question> _questions;

        #region Classes

        class EventData
        {
            public Question CurrentQuestion { get; set; }

            public int CountNotifications { get; set; }

            public List<Prize> Prize { get; set; }

            public Timer Timer { get; set; }

            public DateTime TimeStart { get; set; }
        }

        class PluginConfig
        {
            public int MinPlayers { get; set; }

            public int EventNotificationDelay { get; set; }

            public int MaxNotification { get; set; }

            public int AutoStartDelay { get; set; }

            public List<List<Prize>> Prizes { get; set; }

            public static PluginConfig CreateDefault()
            {
                return new PluginConfig
                {
                    AutoStartDelay = 60,
                    EventNotificationDelay = 60,
                    MinPlayers = 5,
                    MaxNotification = 5,
                    Prizes = new List<List<Prize>>
                    {
                        new List<Prize>
                        {
                            new Prize
                            {
                                Name = "wood",
                                Amount = 10000,
                                Skin = 0
                            }
                        }
                    }
                };
            }
        }

        class Prize
        {
            public string Name { get; set; }

            public int Amount { get; set; }

            public ulong Skin { get; set; }
        }

        class Question
        {
            public bool IsEnabled { get; set; }

            public string Query { get; set; }

            public List<string> Answers { get; set; }
        }

        #endregion

        #region Oxide hooks

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("EventQuiz", _questions);
        }

        void OnServerSave()
        {
            SaveData();
        }


        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Start"] = "<color=#965CF5>[Викторина]:</color> Ответьте на вопрос чтобы получить приз!\n<color=#90EE90>{question}</color>",
                ["End"] = "<color=#965CF5>[Викторина]:</color> <color=#90EE90>{name}</color> первый правильно ответил на вопрос и получил приз!",
                ["Stop"] = "<color=#965CF5>[Викторина]:</color> На вопрос никто не дал ответ. Приз никто не получил!",
                ["Quiz"] = "<color=#965CF5>[Викторина]:</color> Текущий вопрос - <color=#90EE90>{question}</color>",
                ["Notification"] = "<color=#965CF5>[Викторина]:</color> Идёт викторина! Ответьте на вопрос чтобы получить приз!\n<color=#90EE90>{question}</color>",
                ["To Few Players"] = "<color=#965CF5>[Сервер]:</color> Для проведения ивента слишком мало игроков, необходимо минимум <color=#90EE90>{players}</color>",
                ["Already Coming"] = "<color=#965CF5>[Сервер]:</color> Ивент уже идёт",
                ["Event Not Run"] = "<color=#965CF5>[Сервер]:</color> На данный момент викторина не проводится.",
                ["No Question"] = "<color=#965CF5>[Сервер]:</color> Для проведения ивента нет вопросов"
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(PluginConfig.CreateDefault(), true);
            PrintWarning("Default configuration file created.");
        }

        private void OnServerInitialized()
        {
            _config = Config.ReadObject<PluginConfig>();
            _questions = Interface.Oxide.DataFileSystem.ReadObject<List<Question>>("EventQuiz");

            if (_questions.Count == 0)
            {
                _questions = new List<Question>
                {
                    new Question
                    {
                        Query = "It's default question",
                        Answers = new List<string>
                        {
                            "Variant 1",
                            "Variant 2",
                            "Variant 3"
                        }
                    }
                };

                SaveData();
            }

            LoadMessages();
            if (_questions.Where(x => x.IsEnabled).Count() <= 0)
            {
                foreach (var quest in _questions)
                {
                    quest.IsEnabled = true;
                    lang.GetMessage("No Question", this);
                }
            }

            if (_config.AutoStartDelay >= 0)
                _timerAutoStart = timer.Once(_config.AutoStartDelay * 60, () => TryStartEvent());
        }

        object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (player == null || _eventData == null || string.IsNullOrEmpty(message))
                return null;
            var answer = string.Join(" ", message).ToLower().Trim();
            var currentAnswer = _eventData.CurrentQuestion.Answers.FirstOrDefault(x => x.ToLower().Trim() == answer);
            if (string.IsNullOrEmpty(currentAnswer))
                return null;
            GivePrize(player);
            StopEvent();
            Server.Broadcast(lang.GetMessage("End", this).Replace("{name}", player.displayName));
            return null;
        }

        #endregion

        private string TryStartEvent()
        {
            var result = CanStartEvent();
            if (!string.IsNullOrEmpty(result))
            {
                if (_config.AutoStartDelay >= 0)
                    _timerAutoStart = timer.Once(_config.AutoStartDelay * 60, () => TryStartEvent());
                Puts(result);
                return result;
            }
            if (_questions.Where(x => x.IsEnabled).Count() <= 0)
            {
                foreach (var quest in _questions)
                {
                    quest.IsEnabled = true;
                    lang.GetMessage("No Question", this);
                }
            }
            _eventData = new EventData
            {
                CountNotifications = 0,
                CurrentQuestion = GetRandomQuestion(),
                Timer = timer.Repeat(_config.EventNotificationDelay, 0, EventNotification),
                Prize = _config.Prizes[Core.Random.Range(0, _config.Prizes.Count)],
                TimeStart = DateTime.UtcNow
            };

            BasePlayer.activePlayerList.ToList().ForEach(p =>
            {
                SendReply(p, lang.GetMessage("Start", this).Replace("{question}", _eventData.CurrentQuestion.Query));
            });

            return null;
        }

        private void EventNotification()
        {
            if (_eventData.CountNotifications >= _config.MaxNotification)
            {
                _eventData.Timer.Destroy();
                StopEvent();

                BasePlayer.activePlayerList.ToList().ForEach(p =>
                {
                    SendReply(p, lang.GetMessage("Stop", this));
                });

                return;
            }

            BasePlayer.activePlayerList.ToList().ForEach(p =>
            {
                SendReply(p, lang.GetMessage("Notification", this).Replace("{question}", _eventData.CurrentQuestion.Query));
            });

            _eventData.CountNotifications++;
        }

        private string CanStartEvent()
        {
            if (_eventData != null) return lang.GetMessage("Already Coming", this);
            if (BasePlayer.activePlayerList.Count < _config.MinPlayers) return lang.GetMessage("To Few Players", this).Replace("{players}", _config.MinPlayers.ToString());

            if (_questions.Where(x => x.IsEnabled).Count() <= 0)
            {
                foreach (var quest in _questions)
                {
                    quest.IsEnabled = true;
                    lang.GetMessage("No Question", this);
                }
            }

            return null;
        }

        private void StopEvent()
        {
            if (_timerAutoStart != null)
            {
                _timerAutoStart.Destroy();
                _timerAutoStart = null;
            }

            if (_eventData != null && _eventData.Timer != null)
                _eventData.Timer.Destroy();

            _eventData = null;

            if (_config.AutoStartDelay >= 0)
                _timerAutoStart = timer.Once(_config.AutoStartDelay * 60, () => TryStartEvent());
        }

        private void GivePrize(BasePlayer player)
        {
            _eventData.Prize.ForEach(prize =>
            {
                player.GiveItem(ItemManager.CreateByName(prize.Name, prize.Amount, prize.Skin));
            }
            );
        }

        private Question GetRandomQuestion()
        {
            var questions = _questions.Where(x => x.IsEnabled).ToList();
            var question = questions[Core.Random.Range(0, questions.Count)];
            question.IsEnabled = false;
            SaveData();
            return question;
        }

        [ChatCommand("quiz")]
        private void CommandChatRun(BasePlayer player, string cmd, string[] args)
        {
            if (player == null)
                return;
            var result = TryStartEvent();
            if (!string.IsNullOrEmpty(result))
                SendReply(player, result);
            if (args.Length > 0 && args[0].ToLower() == "start" && player.IsAdmin)
            {


                return;
            }

            if (_eventData == null)
            {
                SendReply(player, lang.GetMessage("Event Not Run", this));
                return;
            }
            SendReply(player, lang.GetMessage("Quiz", this).Replace("{question}", _eventData.CurrentQuestion.Query));
        }
    }
}