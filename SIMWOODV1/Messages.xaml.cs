using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


using System.Collections.ObjectModel;
using System.Threading.Tasks;

using Windows.System;

using System.Net.Http;
using System.Net;
using System.Diagnostics;
using Windows.UI.Popups;
using Windows.Storage;
using Windows.Storage.Search;

using Windows.ApplicationModel.Core;

using Windows.UI;
using Windows.UI.ViewManagement;
using Newtonsoft.Json;



// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SIMWOODV1
{

    // Bindable class representing a single text message.
    public class TextMessage
    {
        public string Body { get; set; }
        public string DisplayTime { get; set; }
        public bool IsSent { get; set; }
        public bool IsReceived { get { return !IsSent; } }
    }


    // Observable collection representing a text message conversation
    // that can load more items incrementally.
    public class Conversation : ObservableCollection<TextMessage>, ISupportIncrementalLoading
    {
        // messages in the converstaion
        private uint messageCount = 0;
        public string file;


        // initialise the conversation name
        public Conversation(string source)
        {
            file = source;
        }

        public bool HasMoreItems { get; set; } = true;

        // loads more items from the converstaion
        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            this.CreateMessages(count);

            return Task.FromResult<LoadMoreItemsResult>(
                new LoadMoreItemsResult()
                {
                    Count = count
                }).AsAsyncOperation();
        }

        // loads a number of specified messages with random contents
        private void CreateMessages(uint count)
        {
            // we are going to load messages from the source file 
            // get the source file
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string path = localFolder.Path;
            string file_to_edit = path + "\\" + file + ".txt";
            // we have all the texts in an array now
            var contents = System.IO.File.ReadAllLines(file_to_edit);
            var length = contents.Length;

            for (uint i = 0; i < count; i++)
            {
                // check we havent run out of messages ~ at index 0
                if ((length - 1) - messageCount < 1)
                {
                    HasMoreItems = false;
                    break;
                }

                // which message do we want
                var message = (contents[(length - 1) - messageCount]).Split('~');
                bool sent;
                // decode the message
                if (message[0] == "S") sent = true;
                else sent = false;
                string text = message[1];
                string datetime = message[2];


                this.Insert(0, new TextMessage()
                {
                    Body = text,
                    IsSent = sent,
                    DisplayTime = datetime
                });

                messageCount++;
            }
        }

    }

    // This ListView is tailored to a Chat experience
    public class ChatListView : ListView
    {
        private uint itemsSeen;
        private double averageContainerHeight;
        private bool processingScrollOffsets = false;
        private bool processingScrollOffsetsDeferred = false;

        public ChatListView()
        {
            // We'll manually trigger the loading of data incrementally and buffer for 2 pages worth of data
            this.IncrementalLoadingTrigger = IncrementalLoadingTrigger.None;

            // Since we'll have variable sized items we compute a running average of height to help estimate
            // how much data to request for incremental loading
            this.ContainerContentChanging += this.UpdateRunningAverageContainerHeight;
        }

        protected override void OnApplyTemplate()
        {
            var scrollViewer = this.GetTemplateChild("ScrollViewer") as ScrollViewer;

            if (scrollViewer != null)
            {
                scrollViewer.ViewChanged += (s, a) =>
                {
                    // Check if we should load more data when the scroll position changes.
                    // We only get this once the content/panel is large enough to be scrollable.
                    this.StartProcessingDataVirtualizationScrollOffsets(this.ActualHeight);
                };
            }

            base.OnApplyTemplate();
        }

        // We use ArrangeOverride to trigger incrementally loading data (if needed) when the panel is too small to be scrollable.
        protected override Size ArrangeOverride(Size finalSize)
        {
            // Allow the panel to arrange first
            var result = base.ArrangeOverride(finalSize);

            StartProcessingDataVirtualizationScrollOffsets(finalSize.Height);

            return result;
        }

        private async void StartProcessingDataVirtualizationScrollOffsets(double actualHeight)
        {
            // Avoid re-entrancy. If we are already processing, then defer this request.
            if (processingScrollOffsets)
            {
                processingScrollOffsetsDeferred = true;
                return;
            }

            this.processingScrollOffsets = true;

            do
            {
                processingScrollOffsetsDeferred = false;
                await ProcessDataVirtualizationScrollOffsetsAsync(actualHeight);

                // If a request to process scroll offsets occurred while we were processing
                // the previous request, then process the deferred request now.
            }
            while (processingScrollOffsetsDeferred);

            // We have finished. Allow new requests to be processed.
            this.processingScrollOffsets = false;
        }

        private async Task ProcessDataVirtualizationScrollOffsetsAsync(double actualHeight)
        {
            var panel = this.ItemsPanelRoot as ItemsStackPanel;
            if (panel != null)
            {
                if ((panel.FirstVisibleIndex != -1 && panel.FirstVisibleIndex * this.averageContainerHeight < actualHeight * this.IncrementalLoadingThreshold) ||
                    (Items.Count == 0))
                {
                    var virtualizingDataSource = this.ItemsSource as ISupportIncrementalLoading;
                    if (virtualizingDataSource != null)
                    {
                        if (virtualizingDataSource.HasMoreItems)
                        {
                            uint itemsToLoad;
                            if (this.averageContainerHeight == 0.0)
                            {
                                // We don't have any items yet. Load the first one so we can get an
                                // estimate of the height of one item, and then we can load the rest.
                                itemsToLoad = 1;
                            }
                            else
                            {
                                double avgItemsPerPage = actualHeight / this.averageContainerHeight;
                                // We know there's data to be loaded so load at least one item
                                itemsToLoad = Math.Max((uint)(this.DataFetchSize * avgItemsPerPage), 1);
                            }

                            await virtualizingDataSource.LoadMoreItemsAsync(itemsToLoad);
                        }
                    }
                }
            }
        }

        private void UpdateRunningAverageContainerHeight(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer != null && !args.InRecycleQueue)
            {
                switch (args.Phase)
                {
                    case 0:
                        // use the size of the very first placeholder as a starting point until
                        // we've seen the first item
                        if (this.averageContainerHeight == 0)
                        {
                            this.averageContainerHeight = args.ItemContainer.DesiredSize.Height;
                        }

                        args.RegisterUpdateCallback(1, this.UpdateRunningAverageContainerHeight);
                        args.Handled = true;
                        break;

                    case 1:
                        // set the content
                        args.ItemContainer.Content = args.Item;
                        args.RegisterUpdateCallback(2, this.UpdateRunningAverageContainerHeight);
                        args.Handled = true;
                        break;

                    case 2:
                        // refine the estimate based on the item's DesiredSize
                        this.averageContainerHeight = (this.averageContainerHeight * itemsSeen + args.ItemContainer.DesiredSize.Height) / ++itemsSeen;
                        args.Handled = true;
                        break;
                }
            }
        }
    }

    // Class that runs with the activation of the page
    public sealed partial class Messages : Page
    {

        // global conversation handle
        public Conversation store;
        // global pre-save conversation store
        public List<string> current_conversation = new List<string>();
        // global searchable list of contacts
        public List<string> searchable_contacts = new List<string>();

        // initialise a polling client
        HttpClient PollingClient= new HttpClient();

        // configure an API access
        SIMAPI api_control = new SIMAPI(Constants.API_USERNAME_STORE, Constants.API_PASSWORD_STORE, Constants.API_ACCOUNT_STORE, Constants.API_MOBILE_STORE);

        // initialise the boi
        public Messages()
        {
            this.InitializeComponent();

            //draw into the title bar
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            //remove the solid-colored backgrounds behind the caption controls and system back button
            var viewTitleBar = ApplicationView.GetForCurrentView().TitleBar;
            viewTitleBar.ButtonBackgroundColor = Colors.Transparent;
            viewTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            viewTitleBar.ButtonForegroundColor = (Color)Resources["SystemBaseHighColor"];

            // open the chat

            Load_contacts();

            // sets the source of the chatviews items to the conversation
            //chatView.ItemsSource = this.conversation;

            // adds the handler to the content changing event
            chatView.ContainerContentChanging += OnChatViewContainerContentChanging;

            PollSMSEnable();
        }

        // sends a new text message - linked to the send text message button 
        async void SendTextMessage()
        {
            // if theres text in the message box
            if (MessageTextBox.Text.Length > 0)
            {
                // add a new text message to the converstaion object
                store.Add(new TextMessage
                {
                    Body = MessageTextBox.Text,
                    DisplayTime = DateTime.Now.ToString(),
                    IsSent = true
                });

                // add the text to the conversation store
                current_conversation.Add("S~" + MessageTextBox.Text + "~" + DateTime.Now.ToString());

                // clear the send box
                MessageTextBox.Text = string.Empty;

            }
        }

        // does some alignment of the messages in the chatview
        private void OnChatViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) return;
            TextMessage message = (TextMessage)args.Item;
            args.ItemContainer.HorizontalAlignment = message.IsSent ? Windows.UI.Xaml.HorizontalAlignment.Right : Windows.UI.Xaml.HorizontalAlignment.Left;
        }

        // configures the enter key to also activate send if the message box is selected 
        private void MessageTextBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                this.SendTextMessage();
            }
        }

        // occurs when the edit contact button is pressed
        private async void Edit_Contact(object sender, RoutedEventArgs e)
        {
            // edit a contact
            // get details to preload the boxes
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string path = localFolder.Path;
            string file_to_edit = path + "\\" + Constants.current_contact + ".txt";

            var contents = System.IO.File.ReadAllLines(file_to_edit);

            // decode the number and prefill the boxes
            var key = contents[0].Split(',');
            EContactCompany.Text = key[1];
            EContactNumber.Text = key[0];

            Debug.WriteLine("Before: " + key[0] + key[1]);

            // run the editing dialog
            await EditContactDialog.ShowAsync();
        }

        // performs the saving of edits activate by the pop-up
        private void Save_Edits(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // save the result of the editing dialog
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string path = localFolder.Path;
            string file_to_edit = path + "\\" + Constants.current_contact + ".txt";

            // get the new details
            string number = EContactNumber.Text.ToString();
            string company = EContactCompany.Text.ToString();

            // read in the entire file for editing
            var contents = System.IO.File.ReadAllLines(file_to_edit);

            // edit the contents
            contents[0] = number + ',' + company;

            Debug.WriteLine("After: " + contents[0]);

            // write it back out
            System.IO.File.WriteAllLines(file_to_edit, contents);

        }

        // teh process that deletes contacts activated by a dialog
        private void Delete_Contact_Process(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // pull the name of the file to delete
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string path = localFolder.Path;
            string file_to_delete = path + "\\" + Constants.current_contact + ".txt";
            // nuke it 
            if (System.IO.File.Exists(file_to_delete)) System.IO.File.Delete(file_to_delete);

            // we should clear the conversation
            // at some point

            // we should remove it from the navview
            Load_contacts();
            

        }

        // occurs when delet contact is pressed
        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            // spring up a warning
            ContentDialog DeleteDialog = new ContentDialog()
            {
                Title = "Delete this contact?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                Content = "Are you sure you wish to permanently delete this contact?" 
            };

            DeleteDialog.PrimaryButtonClick += Delete_Contact_Process;

            await DeleteDialog.ShowAsync();
        }

        // change the conversation to the specified person
        public void ChangeConversation(string name)
        {
            Debug.WriteLine(name);
            Conversation conversation = new Conversation(name);
            store = conversation;
            chatView.ItemsSource = store;

        }

        // reload the contacts bar from scratch using the APPdata
        public async void Load_contacts()
        {
            // reset searchable contacts
            searchable_contacts.Clear();

            // clear the view to start again
            naview.MenuItems.Clear();
            // add the new contatc button back

            NavigationViewItem newcon = new NavigationViewItem();
            newcon.Icon = new SymbolIcon(Symbol.Add); ;
            newcon.Name = "AddNewContact";
            newcon.Content = "New Contact";
            naview.MenuItems.Add(newcon);

            // Fill up the contacts with files in the localstate folder as per usual
            // create the directory string
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            // Get the first 20 files in the current folder, sorted by name.
            IReadOnlyList<StorageFile> fil = await localFolder.GetFilesAsync(CommonFileQuery.OrderByName, 0, 500);

            string path = localFolder.Path;
            Debug.WriteLine(path);

            foreach (StorageFile file in fil)
                Debug.WriteLine(file.Name + ", " + file.DateCreated);

            char lastKey = ' ';

            // now lets implement the files in the listings
            foreach (StorageFile file in fil)
            {
                // only get the write ones
                if (file.Name.ToString() != "rememberme.txt")
                {
                    // do some checking on adding letter heading stuff
                    // if we have had a change in the first letter
                    char firstkey = ((file.Name.ToString()[0]));
                    if (firstkey != lastKey)
                    {
                        // add a seperator with the letter of the current contact
                        char letter = (file.Name.ToString()[0]);
                        NavigationViewItemHeader head = new NavigationViewItemHeader();
                        head.Content = letter.ToString(); ;
                        naview.MenuItems.Add(head);

                        lastKey = firstkey;
                    }

                    NavigationViewItem contact = new NavigationViewItem();
                    contact.Name = file.DisplayName.ToString();
                    contact.Icon = new SymbolIcon(Symbol.Contact);
                    contact.Content = file.DisplayName.ToString();

                    naview.MenuItems.Add(contact);
                    searchable_contacts.Add(contact.Name.ToString());

                }
            }
        }

        // ocurs when the selected navview item changes
        private async void naview_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            // wait for the navview to load
            await Task.Delay(700);
            // called when the item in the nav view changes

            NavigationViewItem item = (NavigationViewItem)sender.SelectedItem;

            // if they clicked add new contact then ignore
            if (item.Name == "AddNewContact")
            {
                // new contact was pressed
                return;
            }

            Debug.WriteLine("Contact Selected: " + item.Name);

            // select the new contact to view
            // but only if its not already selected 
            if (Constants.current_contact != item.Name)
            {
                ChangeConversation(item.Name.ToString());
            }

            if (current_conversation.Count > 0)
            {
                // save the conversation to the file
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                string path = localFolder.Path;
                string file_to_append = path + "\\" + Constants.current_contact + ".txt";
                // we have all the texts in an array now
                System.IO.File.AppendAllText(file_to_append, "\n");
                System.IO.File.AppendAllLines(file_to_append, current_conversation);

                current_conversation.Clear();
            }

            // resets any notifications
            item.Icon = new SymbolIcon(Symbol.Contact);

            // sets the title 
            Description.Text = item.Name;
            Constants.current_contact = item.Name;

        }

        // ocurs everytime a navview item is pressed
        private async void naview_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            // wait for the navview to load
            await Task.Delay(700);
            // called when an item in the navview is invoked
            // this is only for adding contacts

            NavigationViewItem item = (NavigationViewItem)sender.SelectedItem;

            if (item.Name != "AddNewContact")
            {
                // something else has been pressed
                return;
            }

            Debug.WriteLine("Creating New Contact...");

            // run the new contact code

            await NewContactDialog.ShowAsync();

            // save the result of the dialog 
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string path = localFolder.Path;
            string file_to_edit = path + "\\" + NewContactName.Text.ToString() + ".txt";

            // get the new details
            string number = NewContactNumber.Text.ToString();
            string company = NewContactCompany.Text.ToString();

            // read in the entire file for editing
            string contents = number + ',' + company;

            // write it back out
            await System.IO.File.WriteAllTextAsync(file_to_edit, contents);

            // wait alittle for the FS
            await Task.Delay(2000);

            // reload the contacts
            Load_contacts();
        }

        // AUTOSUGGEST text in the box has changed
        private void TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Only get results when it was a user typing, 
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                //Set the ItemsSource to be your filtered dataset
                //sender.ItemsSource = dataset;

                // ALGORITHM
                // - we will prioritise substrings of the available search
                // - after that we will add the lowest levenshtein distance results up to a constant
                // - max 8

                List<string> matched = new List<string>();
                string source = sender.Text;
                int results = 0;

                // go though each member of the contacts and check it 
                foreach (string test in searchable_contacts)
                {
                    // if the contact contains what weve typed do far
                    if (test.Contains(source))
                    {
                        matched.Add(test);
                        results++;
                    }
                    // if we have enough get out
                    if (results > 5) break;
                }

                // if we're running out of results we can get some with small errors
                if (results < 5)
                {
                    // go though each member of the contacts and check it 
                    foreach (string test in searchable_contacts)
                    {
                        // we want to check only up to the characters that have been typed
                        string test_s;
                        try
                        {
                             test_s = test.Substring(0, source.Length);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            // the search text is longer than the name oh well
                             test_s = test;
                        }

                        // if the difference number is less than 3 and its not already in
                        if (LevenshteinDistance(source, test_s) < 3 && !matched.Contains(test))
                        {
                            matched.Add(test);
                            results++;
                        }
                        // if we have enough get out
                        if (results > 7) break;
                    }
                }

                sender.ItemsSource = matched;
            }
        }

        // AUTOSUGGEST one of the suggestions is chosen/ highlighted
        private void SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            // Set sender.Text. You can use args.SelectedItem to build your text string.
            sender.Text = args.SelectedItem.ToString();
        }

        // AUTOSUGGEST the box has ben queried
        private void QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null)
            {
                // we have to go through each and compare them 
                int index = 0;
                foreach (object n in naview.MenuItems)
                {
                    try
                    {
                        string item = ((NavigationViewItem)n).Name.ToString();
                        if (item == args.ChosenSuggestion.ToString()) break;
                    }
                    catch (System.InvalidCastException e)
                    {
                        // it was a header or something else ~ oh well ~ fml
                    }
                    index++;
                }

                naview.SelectedItem = naview.MenuItems[index];
            }
            else
            {
                // Use args.QueryText to determine what to do.
            }
        }

        // search computation
        public static int LevenshteinDistance(string src, string dest)
        {
            int[,] d = new int[src.Length + 1, dest.Length + 1];
            int i, j, cost;
            char[] str1 = src.ToCharArray();
            char[] str2 = dest.ToCharArray();

            for (i = 0; i <= str1.Length; i++)
            {
                d[i, 0] = i;
            }
            for (j = 0; j <= str2.Length; j++)
            {
                d[0, j] = j;
            }
            for (i = 1; i <= str1.Length; i++)
            {
                for (j = 1; j <= str2.Length; j++)
                {

                    if (str1[i - 1] == str2[j - 1])
                        cost = 0;
                    else
                        cost = 1;

                    d[i, j] =
                        Math.Min(
                            d[i - 1, j] + 1,              // Deletion
                            Math.Min(
                                d[i, j - 1] + 1,          // Insertion
                                d[i - 1, j - 1] + cost)); // Substitution

                    if ((i > 1) && (j > 1) && (str1[i - 1] ==
                        str2[j - 2]) && (str1[i - 2] == str2[j - 1]))
                    {
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + cost);
                    }
                }
            }

            return d[str1.Length, str2.Length];
        }

        // add a message in the background 
        public async void AddRecievedSMS(string message, string datetime, string number)
        {
            // should we notify 
            bool notify = false;
            string who = "";

            // so first we need to figure out who the number is registered to
            string contact = " ";

            // create the directory string
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            // Get the first 20 files in the current folder, sorted by name.
            IReadOnlyList<StorageFile> fil = await localFolder.GetFilesAsync(CommonFileQuery.OrderByName, 0, 500);

            foreach (StorageFile file in fil)
            {
                // read the file
                string[] lines = System.IO.File.ReadAllLines(file.Path);
                // get the number out of it
                string matchnum = lines[0].Split(',')[1];
                // compare teh contact to the recieved contact
                if (matchnum == number)
                {
                    // we have the contact
                    contact = file.DisplayName;
                    break;
                }
            }

            // if we found a contact
            if (contact!=" ")
            {
                // add the message to the existing contact

                // check if we are currently viewing the converstaion
                try
                {
                    NavigationViewItem item = (NavigationViewItem)naview.SelectedItem;
                    if (item.Name == contact)
                    {
                        // we are looking at the converation currently
                        // add to the live conversation and leave
                        // add a new text message to the converstaion object
                        store.Add(new TextMessage
                        {
                            Body = message,
                            DisplayTime = datetime,
                            IsSent = false
                        });
                        return;
                    }
                }
                catch (InvalidCastException)
                {
                    // edge case where we arent looking at anything 
                    // add to file anyway
                }

                // we have left if it was teh live convo so add to the file

                string path = localFolder.Path;
                string file_to_append = path + "\\" + contact + ".txt";
                // we have all the texts in an array now
                System.IO.File.AppendAllText(file_to_append, "\n");
                List<string> temp = new List<string>();
                temp.Add("R~" + message + "~" + datetime);
                System.IO.File.AppendAllLines(file_to_append, temp);

                notify = true;
                who = contact;

            }
            else
            {
                // we have to make a new blank contact and add the file

                string path = localFolder.Path;
                string file_to_edit = path + "\\" + datetime + ".txt";

                // read in the entire file for editing
                string contents = number + ',' + "None\nR~" + message + "~" + datetime;

                // write it back out
                await System.IO.File.WriteAllTextAsync(file_to_edit, contents);

                // wait alittle for the FS
                await Task.Delay(2000);

                // reload the contacts
                Load_contacts();

                notify = true;
                who = datetime;
            }

            // so weve added teh text, do we need to notify the user? (is it background)
            if (notify)
            {
                // we have to go through each and compare them 
                int index = 0;
                foreach (object n in naview.MenuItems)
                {
                    try
                    {
                        string item = ((NavigationViewItem)n).Name.ToString();
                        if (item == who) break;
                    }
                    catch (System.InvalidCastException e)
                    {
                        // it was a header or something else ~ oh well ~ fml
                    }
                    index++;
                }

                // we have the item that needs a notification
                // give it a star
                NavigationViewItem itemhandle = (NavigationViewItem)naview.MenuItems[index];
                itemhandle.Icon = new SymbolIcon(Symbol.SolidStar);

            }
        }

        // sms polling handler
        public async void PollingSMSAsync(object sender, object e)
        {
            // poll the server
            string path = "http://54.38.214.180/?number=" + Constants.API_MOBILE_STORE.ToString();
            HttpResponseMessage response = await PollingClient.GetAsync(path);

            // if the server had something for us
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine(content);
                SMSinboundBETA sms = JsonConvert.DeserializeObject<SMSinboundBETA>(content);
                AddRecievedSMS(sms.data.message, sms.data.time, sms.data.origintor);
                Debug.WriteLine("Successfully Pulled Text: ");
                Debug.WriteLine("From: " + sms.data.origintor);
                Debug.WriteLine("Message: " + sms.data.message);
                Debug.WriteLine("DT: " + sms.data.time);
                return;
            }

            // the server had nothing for us
            Debug.WriteLine("No Messages for us :((");

        }

        // ASYNC POLL FOR SMS'S MY DUDE
        public void PollSMSEnable()
        {
            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += PollingSMSAsync;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 5);
            //IsEnabled defaults to false
            dispatcherTimer.Start();
            //IsEnabled should now be true after calling start
        }
    }
}
