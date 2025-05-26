using System;
using System.Collections.Generic;
using System.Text;

namespace Projet.View
{
    public interface IView
    {
        void Show();
        void Close();
    }
    public interface IMainView : IView { }
    public interface IAddJobView : IView { }
    public interface IRemoveJobView : IView { }
}
