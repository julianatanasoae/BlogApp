using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace kraigb
{
    // J: I had to create this because the ZoomedOutView in the SemanticZoom control
    // did not allow me to create ItemClick events
    public class SemanticListViewWrapper : Grid, ISemanticZoomInformation
    {
        public void CompleteViewChange()
        {
            //throw new NotImplementedException();
        }

        public void CompleteViewChangeFrom(SemanticZoomLocation source, SemanticZoomLocation destination)
        {
            //throw new NotImplementedException();
        }

        public void CompleteViewChangeTo(SemanticZoomLocation source, SemanticZoomLocation destination)
        {
            //throw new NotImplementedException();
        }

        public void InitializeViewChange()
        {
            //throw new NotImplementedException();
        }

        public bool IsActiveView
        {
            get;
            set;
        }

        public bool IsZoomedInView
        {
            get;
            set;
        }

        public void MakeVisible(SemanticZoomLocation item)
        {
            //throw new NotImplementedException();
        }

        public SemanticZoom SemanticZoomOwner
        {
            get;
            set;
        }

        public void StartViewChangeFrom(SemanticZoomLocation source, SemanticZoomLocation destination)
        {
            //throw new NotImplementedException();
        }

        public void StartViewChangeTo(SemanticZoomLocation source, SemanticZoomLocation destination)
        {
            //throw new NotImplementedException();
        }
    }
}
