using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace FireflyWindows
{
    class Tube : INotifyPropertyChanged
    {
        string name;
        bool fired;
        int number;
        Color color;

        public string Name
        {
            get { return name; }
            set { name = value; OnPropertyChanged("Name"); }
        }

        public bool Fired
        {
            get { return fired; }
            set { fired = value; OnPropertyChanged("Fired"); }
        }

        public int Number
        {
            get { return number; }
            set { number = value; OnPropertyChanged("Number"); }
        }

        public Color Color
        {
            get { return color; }
            set { color = value; OnPropertyChanged("Color"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
