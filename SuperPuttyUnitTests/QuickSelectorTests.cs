using System.Collections.Generic;
using SuperPutty.Gui;
using SuperPutty.Data;
using System.Drawing;
using SuperPutty;

namespace SuperPuttyUnitTests
{
    //[TestFixture]
    public class QuickSelectorTests
    {

        [TestView]
        public void Test()
        {
            List<SessionData> sessions = SessionData.LoadSessionsFromFile("c:/Users/beau/SuperPuTTY/sessions.xml");
            QuickSelectorData data = new QuickSelectorData();

            foreach (SessionData sd in sessions)
            {
                data.ItemData.AddItemDataRow(
                    sd.SessionName, 
                    sd.SessionId, 
                    sd.Proto == ConnectionProtocol.Cygterm || sd.Proto == ConnectionProtocol.Mintty ? Color.Blue : Color.Black, null);
            }

            QuickSelectorOptions opt = new QuickSelectorOptions
            {
                Sort = data.ItemData.DetailColumn.ColumnName,
                BaseText = "Open Session"
            };

            QuickSelector d = new QuickSelector();
            d.ShowDialog(null, data, opt);
        }


    }
}
