﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;
using Led.ViewModels;

namespace Led.Services
{
    public class EffectService : IEffectService, Interfaces.IParticipant
    {
        /* The EffectService recieves the AudioTimer Message and updates all Entitys (AudioControlCurrentTick)
         * -> Maybe provide Async Update so the AudioControl won't get disturbed
         * -> Provides functionality to jump to a position in the timeline -> Check
         * -> Maybe just switch to a long instead of AudioControlCurrentTick in case of performance issues
         * 
         * The Entities must register themselves on construction
         * 
         * Implement different Updates
         *      Update the View in the App -> Check
         *      Update the Suit
         * -> Only the visible view will get updated
         * 
         * Provides functionality to test an effect in the view and the suit
         * -> Must implement a Timer (like AudioControl)
         * 
         * Provides functionality to pre render all effects and save them in the model (model/vm?)
         * */
        private Services.MediatorService _Mediator;

        private List<LedEntityBaseVM> _Entities;
        private LedEntityBaseVM _CurrentLedEntity;

        private long _LastTickedFrame;
        private long _LastRecievedFrame;

        private Timer _DispatcherTimer;

        public void RegisterEntity(LedEntityBaseVM ledEntity)
        {
            if (!_Entities.Contains(ledEntity))
                _Entities.Add(ledEntity);
        }

        public void RenderEntity(LedEntityBaseVM ledEntity)
        {
            List<IEffectLogic> _Effects = new List<IEffectLogic>();
            ledEntity.LedEntity.Effects.ForEach(x => _Effects.Add(x as IEffectLogic));

            foreach (var Effect in _Effects)
            {
                if (Effect.Active)
                {
                    for (ushort i = Effect.StartFrame; i < Effect.EndFrame; i++)
                    {
                        ledEntity.LedEntity.Seconds[i / 60].Frames[i % 60].LedChanges.AddRange(Effect.LedChangeDatas);
                    }
                }
            }
        }

        public void RenderAllEntities()
        {
            _Entities.ForEach(x => RenderEntity(x));
        }

        public void Init(LedEntityBaseVM ledEntity)
        {
            _Entities.Clear();
            _CurrentLedEntity = ledEntity;
        }

        public EffectService()
        {
            _Entities = new List<LedEntityBaseVM>();

            _DispatcherTimer = new Timer();
            _DispatcherTimer.AutoReset = true;
            _DispatcherTimer.Interval = 1000 / Defines.FramesPerSecond;
            _DispatcherTimer.Elapsed += _DispatcherTimerTick;

            _Mediator = App.Instance.MediatorService;
            _Mediator.Register(this);
        }

        private void _DispatcherTimerTick(object sender, EventArgs e)
        {
            _LastTickedFrame += 1;
            Debug.WriteLine("DispatcherTick: " + _LastTickedFrame.ToString().PadLeft(10));
        }

        private void _SynchronizeWithMusic()
        {
            Debug.WriteLine("MusicTick: " + _LastRecievedFrame.ToString().PadLeft(10));
        }

        private void _UpdateView(LedEntityBaseVM ledEntity, long currentFrame)
        {
            //Get refs and compute the current second
            int currentSecond = (int)(currentFrame / Defines.FramesPerSecond);
            int currentFramesRespectiveCurrentSecond = (int)(currentFrame % Defines.FramesPerSecond);
            List<Model.Second> seconds = ledEntity.LedEntity.Seconds;

            //Just some checking
            if (currentSecond >= seconds.Count)
            {
                Console.WriteLine("UpdateFrame out of range. FrameValue: " + currentFrame + " SecondValue: " + currentSecond + "MaxSeconds: " + seconds.Count);
                return;
            }

            //When we are only one frame ahead of the ledEntity (normal) just execute the update
            if (currentFrame - 1 == ledEntity.CurrentFrame)
            {
                ledEntity.SetLedColor(seconds[currentSecond].Frames[currentFramesRespectiveCurrentSecond].LedChanges);
            }
            else
            {
                //When we aren't in the current Second, load the full image
                if (ledEntity.CurrentFrame / Defines.FramesPerSecond == currentSecond)
                {
                    ledEntity.SetLedColor(ledEntity.LedEntity.Seconds[currentSecond].LedEntityStatus);
                }

                //After this execute all FrameChanges till the current one
                List<List<Model.LedChangeData>> _LedChangeDatas = new List<List<Model.LedChangeData>>();
                for (int i = (int)(ledEntity.CurrentFrame % Defines.FramesPerSecond) + 1; i <= currentFramesRespectiveCurrentSecond; i++)
                {
                    _LedChangeDatas.Add(ledEntity.LedEntity.Seconds[currentSecond].Frames[i].LedChanges);
                }

                ledEntity.SetLedColor(_CumulateLedChangeDatas(_LedChangeDatas));
            }

            ledEntity.CurrentFrame = currentFrame;
        }

        private void _UpdateSuit(LedEntityBaseVM ledEntity, long currentFrame)
        {

        }

        /// <summary>
        /// Cumulates ledChangeDatas so that every LED is refreshed only once.
        /// </summary>
        /// <param name="ledChangeDatas">New list of ledChangeDatas starting with the latest.</param>
        /// <returns></returns>
        private List<Model.LedChangeData> _CumulateLedChangeDatas(List<List<Model.LedChangeData>> ledChangeDatas)
        {
            List<Model.LedChangeData> _LedChangeDatas = new List<Model.LedChangeData>();
            List<Utility.LedModelID> _LedIDs = new List<Utility.LedModelID>();
            for (int i = 0; i < ledChangeDatas.Count; i++)
            {
                ledChangeDatas[i].ForEach(x => _LedIDs.AddRange(x.LedIDs));

                for (int j = 0; j < ledChangeDatas.Count - i; j++)
                {
                    ledChangeDatas[i + j].ForEach(x => x.LedIDs.RemoveAll(LedID => _LedIDs.Contains(LedID)));
                }

                _LedChangeDatas.AddRange(ledChangeDatas[i]);
            }

            return _LedChangeDatas;
        }

        private void _SendFrameDataToSuit(LedEntityBaseVM ledEntity)
        {

        }

        public void RecieveMessage(MediatorMessages message, object sender, object data)
        {
            switch (message)
            {
                case MediatorMessages.AudioControlPlayPause:
                    _LastRecievedFrame = (data as MediatorMessageData.AudioControlPlayPauseData).CurrentFrame;
                    _LastTickedFrame = _LastRecievedFrame;
                    if ((data as MediatorMessageData.AudioControlPlayPauseData).Playing)
                        _DispatcherTimer.Start();
                    else
                        _DispatcherTimer.Stop();
                    break;
                case MediatorMessages.AudioControlCurrentTick:
                    long _frameRecieved = (data as MediatorMessageData.AudioControlCurrentFrameData).CurrentFrame;
                    if (_LastRecievedFrame != _frameRecieved)
                    {
                        _LastRecievedFrame = _frameRecieved;
                        _SynchronizeWithMusic();
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
