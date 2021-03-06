﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Led.ViewModels
{
    class NewProjectDialogVM : DialogVM
    {

        private string _dialogTitle;
        public string DialogTitle
        {
            get => _dialogTitle;
            set
            {
                if (_dialogTitle != value)
                {
                    _dialogTitle = value;
                    RaisePropertyChanged(nameof(DialogTitle));
                }
            }
        }


        private string _textToEnter;
        public string TextToEnter
        {
            get => _textToEnter;
            set
            {
                if (_textToEnter != value)
                {
                    _textToEnter = value;
                    RaisePropertyChanged(nameof(TextToEnter));
                }
            }
        }

        private string _text;
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    RaisePropertyChanged(nameof(Text));
                }
            }
        }

        public Command OkCommand { get; set; }
        public Command AbortCommand { get; set; }
        public Command CloseWindowCommand { get; set; }

        public NewProjectDialogVM(string dialogTitle = "Neues Projekt anlegen", string textToEnter = "Bitte Namen eingeben:")
        {
            DialogTitle = dialogTitle;
            TextToEnter = textToEnter;
            OkCommand = new Command(_OnOkCommand);
            AbortCommand = new Command(_OnAbortCommand);
            CloseWindowCommand = new Command(_OnCloseWindowCommand);
        }

        private void _OnCloseWindowCommand()
        {
            App.Instance.WindowService.CloseWindow(this);
        }
    }
}
