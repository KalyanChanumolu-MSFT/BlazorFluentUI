﻿using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace BlazorFabric
{
    public class GlobalCS : ComponentBase, IGlobalCSSheet, IDisposable, INotifyPropertyChanged
    {
        [Inject]
        public IComponentStyle ComponentStyle { get; set; }

        [Parameter]
        public ICollection<Rule> Rules
        {
            get => _rules;
            set 
            {
                _rules = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Rules"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private ICollection<Rule> _rules;

        public void Dispose()
        {
            ComponentStyle.GlobalCSSheets.Remove(this);
        }

        protected override Task OnInitializedAsync()
        {
            ComponentStyle.GlobalCSSheets.Add(this);
            return base.OnInitializedAsync();
        }

        protected override void OnParametersSet()
        {
            base.OnParametersSet();
        }
    }
}
