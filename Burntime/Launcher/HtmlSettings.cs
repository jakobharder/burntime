using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Burntime.Platform.IO;

namespace Burntime.Launcher
{
    class HtmlSettings
    {
        ConfigFile settings;

        public HtmlSettings(ConfigFile settings)
        {
            this.settings = settings;
        }

        // write settings to html page
        public void UpdateHtml(HtmlDocument document, bool setHandler)
        {
            foreach (HtmlElement element in document.GetElementsByTagName("option"))
            {
                if (setHandler)
                {
                    // add click handler
                    element.Click += new HtmlElementEventHandler(OnClick);
                }

                if (CheckSettingsConditions(element.GetAttribute("value")))
                {
                    element.SetAttribute("selected", "true");
                }
            }

            foreach (HtmlElement element in document.GetElementsByTagName("input"))
            {
                if (element.GetAttribute("type").Equals("checkbox", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (setHandler)
                    {
                        // add click handler
                        element.Click += new HtmlElementEventHandler(OnClick);
                    }

                    if (CheckSettingsConditions(element.GetAttribute("value")))
                        element.SetAttribute("checked", "true");
                }
            }

            // disable control if depended package is not available
            foreach (HtmlElement element in document.All)
            {
                if (element.GetAttribute("depend") != "")
                {
                    if (null == Program.PackageManager.GetInfo(element.GetAttribute("depend")))
                        element.SetAttribute("disabled", "true");
                }
            }

            UpdateControls(document);
        }

        // update control states
        public void UpdateControls(HtmlDocument document)
        {
            foreach (HtmlElement element in document.GetElementsByTagName("input"))
            {
                if (element.GetAttribute("type").Equals("checkbox", StringComparison.InvariantCultureIgnoreCase))
                {
                    string disable = element.GetAttribute("disable");
                    string checkon = element.GetAttribute("checkon");
                    string checkoff = element.GetAttribute("checkoff");

                    bool isElementChecked = element.GetAttribute("checked").Equals("true", StringComparison.InvariantCultureIgnoreCase);
                    HtmlElement el = element.Document.GetElementById(disable);
                    if (el != null)
                        el.SetAttribute("disabled", isElementChecked ? "true" : "");
                    el = element.Document.GetElementById(checkon);
                    if (el != null && isElementChecked)
                        el.SetAttribute("checked", "true");
                    el = element.Document.GetElementById(checkoff);
                    if (el != null && isElementChecked)
                        el.SetAttribute("checked", "true");
                }
            }
        }

        // read settings from html page
        public void UpdateData(HtmlDocument document)
        {
            // save selection
            foreach (HtmlElement element in document.GetElementsByTagName("option"))
            {
                string selected = element.GetAttribute("selected");
                if (selected.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                {
                    SetSettings(element.GetAttribute("value"));
                }
            }
            // save checkboxes
            foreach (HtmlElement element in document.GetElementsByTagName("input"))
            {
                if (element.GetAttribute("type").Equals("checkbox", StringComparison.InvariantCultureIgnoreCase))
                {
                    string check = element.GetAttribute("checked");
                    if (check.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                        SetSettings(element.GetAttribute("value"));
                    else
                        SetSettings(element.GetAttribute("not"));
                }
            }
        }

        private void OnClick(object sender, HtmlElementEventArgs args)
        {
            HtmlElement element = sender as HtmlElement;
            UpdateControls(element.Document);
        }

        private bool CheckSettingsConditions(string conditions)
        {
            string[] values = conditions.Split(';');

            foreach (string condition in values)
            {
                string[] key_value = condition.Trim().Split('=');
                if (key_value.Length != 2)
                    return false;

                string key = key_value[0].Trim();
                string val = key_value[1].Trim();

                string[] key_section = key.Split('@');
                if (key_section.Length != 2)
                    return false;

                key = key_section[0];
                string section = key_section[1];

                string user_value = settings[section].Get(key);

                if (!user_value.Equals(val, StringComparison.InvariantCultureIgnoreCase))
                    return false;
            }

            return true;
        }

        private void SetSettings(string settings)
        {
            string[] values = settings.Split(';');

            foreach (string condition in values)
            {
                string[] key_value = condition.Trim().Split('=');
                if (key_value.Length != 2)
                    return;

                string key = key_value[0].Trim();
                string val = key_value[1].Trim();

                string[] key_section = key.Split('@');
                if (key_section.Length != 2)
                    return;

                key = key_section[0];
                string section = key_section[1];

                this.settings[section].Set(key, val);
            }
        }
    }
}
