﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KraxbotOSS
{
    public partial class FormChatroomInfo : Form
    {
        public FormChatroomInfo(Form1.Settings settings)
        {
            InitializeComponent();
            
            Text = settings.ChatName;

            Set(lInvitedName, settings.InvitedName);
            Set(lSpam,        settings.Spam);
            Set(lWelcomeMsg,  settings.WelcomeMsg);

            Set(lCleverbot,     settings.Cleverbot);
            Set(lCommands,      settings.Commands);
            Set(lDcKick,        settings.DCKick);
            Set(lWelcome,       settings.Welcome);
            Set(lGames,         settings.Games);
            Set(lDefine,        settings.Define);
            Set(lWiki,          settings.Wiki);
            Set(lSearch,        settings.Search);
            Set(lWeather,       settings.Weather);
            Set(lStore,         settings.Store);
            Set(lResponses,     settings.Responses);
            Set(lLinkResolving, settings.Links);
            Set(lRules,         settings.Rules);
            Set(lAllPoke,       settings.AllPoke);

            Set(lDelayRandom,  settings.DelayRandom);
            Set(lDelayDefine,  settings.DelayDefine);
            Set(lDelayGames,   settings.DelayGames);
            Set(lDelayRecents, settings.DelayRecents);
            Set(lDelaySearch,  settings.DelaySearch);
            Set(lYtDelay,      settings.DelayYT);
        }

        void Set(Label label, string setting)
        {
            label.Text = setting;
        }
        void Set(Label label, bool setting)
        {
            if (setting) label.Text = "Enabled";
            else label.Text = "Disabled";
        }
        void Set(Label label, int delay)
        {
            // Assume it's for delays
            label.Text = delay + " s";
        }
    }
}
