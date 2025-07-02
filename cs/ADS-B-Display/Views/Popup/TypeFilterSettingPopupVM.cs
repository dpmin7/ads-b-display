using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ADS_B_Display.Views.Popup
{
    internal class TypeFilterSettingPopupVM : NotifyPropertyChangedBase
    {
        private Action<List<string>> CheckedListCB = null;

        public ObservableCollection<TypeItem> AircraftTypeList { get; set; } = new ObservableCollection<TypeItem>();
        public TypeFilterSettingPopupVM(IList<string> AircraftTypeList, Action<List<string>> checkedListCB)
        {
            this.AircraftTypeList = new ObservableCollection<TypeItem>( AircraftTypeList.Select(x => new TypeItem(x, false, ChangeData)));
            CheckedListCB = checkedListCB;

            Cmd_Apply = new DelegateCommand(Apply);
        }

        private void ChangeData(TypeItem item)
        {
            if (item.IsChecked)
            {
                SelectedAircraftTypeList.Add(item);
            }
            else
            {
                SelectedAircraftTypeList.Remove(item);
            }
            VisibleSelectedList = SelectedAircraftTypeList.Count != 0;
        }

        private void Apply(object obj)
        {
            CheckedListCB?.Invoke(AircraftTypeList.Where(x => x.IsChecked).Select(x => x.Type).ToList());
        }

        public ICommand Cmd_Apply { get; }

        private ObservableCollection<TypeItem> _SelectedAircraftTypeList = new ObservableCollection<TypeItem>();
        public ObservableCollection<TypeItem> SelectedAircraftTypeList
        {
            get => _SelectedAircraftTypeList;
            set
            {
                _SelectedAircraftTypeList = value;
                OnPropertyChanged(nameof(SelectedAircraftTypeList));
            }
        }

        private bool visibleSelectedList;
        public bool VisibleSelectedList { get => visibleSelectedList; set => SetProperty(ref visibleSelectedList, value); }
    }

    public class TypeItem : NotifyPropertyChangedBase
    {
        private Action<TypeItem> SendMyself;
        public string Type { get; set; }
        private bool isChecked;
        public bool IsChecked {
            get => isChecked;
            set
            {
                isChecked = value;
                OnPropertyChanged(nameof(IsChecked));
                SendMyself?.Invoke(this);
            }
        }
        public TypeItem(string type, bool isChecked, Action<TypeItem> sendMyself)
        {
            Type = type;
            IsChecked = isChecked;
            SendMyself = sendMyself;
        }
    }
}
