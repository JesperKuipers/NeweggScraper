﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using NeweggScraper.Utils.Mail;
using Google.Apis.Gmail.v1.Data;
using Label = Google.Apis.Gmail.v1.Data.Label;
using Thread = Google.Apis.Gmail.v1.Data.Thread;
using System.Windows.Threading;
using Microsoft.Win32;
using NeweggScraper.Utils;
using NeweggScraper.View;
using NeweggScraper.ViewModels;
using Telegram.Bot;
using WMPLib;


namespace NeweggScraper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Scraper scraper;
        private Scraper prevScraper;
        private NotifyMeViewModel _notifyMe = new NotifyMeViewModel();
        //public NotifyMe n = new NotifyMe();
        public Email mail = new Email();
        private int loopTimeInt;
        private MediaPlayer mp = new MediaPlayer();

        public static string mailListt = "";
        public static string botToken = "";
        public static string telegramChannel = "";
        public static string DiscordBotToken = "";
        public static int DicordLoopDelay;



        public static string SearchingIn1 { get; set; }

        public static MainWindow Instance { get; private set; }


        public MainWindow()
        {
            InitializeComponent();
            scraper = new Scraper();
            DataContext = scraper;
            Instance = this;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }


        // Scrape Site Data
        private async void BtnScraper_Click(object sender, RoutedEventArgs e)
        {

            if (!string.IsNullOrEmpty(UrlInput.Text))
            {
                Run.IsEnabled = false;
                if (runInLoop.IsChecked ?? false)
                    running.Text = "Running in a loop...";
                else
                    running.Text = "Running...";

                running.Visibility = Visibility.Visible;

                await Task.Delay(2000);

                if (!IsAnumber(loopTime.Text) && loopTime.IsEnabled)
                {
                    running.Text = "Stopped";
                    Run.IsEnabled = true;
                    runInLoop.IsChecked = false;
                    return;
                }

                try
                {
                    int discordDelay = 0;
                    var msg = "";
                    string prevMsg = "";
                    var curMsg = "";


                    do
                    {
                        SearchFilters.Children.Clear();

                        var a = notifyMeUC.DataContext.ToString().Equals("NeweggScraper.ViewModels.NotifyMeViewModel");
                        scraper.InStockEntries.Clear();
                        scraper.OutOfStockEntries.Clear();
                        scraper.ScrapeData(UrlInput.Text);

                        // store current results in order to compare wit prev
                        foreach (var item in scraper.InStockEntries)
                        {
                            curMsg += item.Link;
                        }

                        //await Task.Delay(1000);
                        inStockLabel.Text = $"{scraper.InStockEntries.Count} items found In Stock";
                        outOfStockLabel.Text = $"{scraper.OutOfStockEntries.Count} items found Out Of Stock";

                        if (!a)
                        {
                            DataContext = scraper;
                            SearchCategoryPanel.Visibility = Visibility.Visible;
                            SearchFiltersHeader.Visibility = Visibility.Visible;
                            SearchingBorder.Visibility = Visibility.Visible;
                            SearchFiltersScroller.Visibility = Visibility.Visible;
                            SearchingIn.Text = SearchingIn1;
                        }


                        if (scraper.InStockEntries.Count > 0)
                        {


                            var cartLink = "";
                            string[] link = null;
                            List<string> msgs = new List<string>();


                            if (NotifyMe.NotifyTelegram || NotifyMe.NotifyDiscord)
                            {

                                foreach (var item in scraper.InStockEntries)
                                {
                                    try
                                    {
                                        if (!item.Link.ToLower().Contains("combodeal"))
                                        {
                                            link = item.Link.Split('?');
                                            var cart = link[0].Split('/');
                                            for (int i = 0; i < cart.Length; i++)
                                            {
                                                if (cart[i] == "p")
                                                {
                                                    cartLink = cart[i + 1];
                                                }
                                            }
                                        }
                                        else
                                        {
                                            link = item.Link.Split('?');
                                            var cart = item.Link.Split('=');
                                            cartLink = cart[1];
                                        }
                                    }
                                    catch (Exception exception)
                                    {

                                    }

                                    if (!item.Link.ToLower().Contains("combodeal"))
                                    {
                                        msgs.Add($"{item.Brand} \n\n{item.Description}\n\n{item.Price}\n\n{link[0]} \n\nAdd To Cart\n{item.AddToCart}\n-----------------------------------------------------\n");
                                    }
                                    else
                                    {
                                        msgs.Add($"{item.Brand} \n\n{item.Description}\n\n{item.Price}\n\n{item.Link} \n\nAdd To Cart\n{item.AddToCart}\n-----------------------------------------------------\n");
                                    }

                                    // Discord Notification
                                    if (NotifyMe.NotifyDiscord)
                                    {
                                        if (discordDelay == 0 || curMsg != prevMsg)
                                        {
                                            Discord.SendDiscord(DiscordBotToken, item);
                                            await Task.Delay(500);
                                        }

                                        if (discordDelay == DicordLoopDelay)
                                        {
                                            discordDelay = -1;
                                        }
                                    }
                                }
                                discordDelay++;

                                // Send message to Telegram channel
                                if (NotifyMe.NotifyTelegram)
                                {
                                    var bot = new TelegramBotClient(botToken);
                                    // handling telegram too long msg limit
                                    int ind = 0;
                                    for (int i = 0; ind < msgs.Count; i++)
                                    {
                                        string newMsg = "";
                                        for (int j = 0; j <= 8; j++)
                                        {
                                            if (ind < msgs.Count)
                                                newMsg += msgs[ind];
                                            ind++;
                                        }
                                        var s = await bot.SendTextMessageAsync(telegramChannel, newMsg);
                                    }
                                }
                            }


                            // Make a sound
                            if (NotifyMe.NotifySound)
                            {
                                var path = Directory.GetCurrentDirectory() + "\\SoundFile\\notifysound.mp3";
                                OpenFileDialog openFileDialog = new OpenFileDialog();
                                openFileDialog.Filter = "MP3 files (*.mp3)|*.mp3|All files (*.*)|*.*";
                                openFileDialog.FileName = path;
                                if (path.Equals(openFileDialog.FileName))
                                {
                                    mp.Open(new Uri(openFileDialog.FileName));
                                    mp.Play();
                                }
                            }

                            // Send Email
                            if (NotifyMe.Notify)
                            {
                                var m = mailListt.Replace("\n", ", ");
                                mail.SendEmail(m);
                            }
                            Scraper.SearchResults = "";
                        }

                        if (curMsg != "")
                            prevMsg = curMsg;
                        curMsg = "";
                        prevScraper = new Scraper(scraper);

                        if (runInLoop.IsChecked ?? false)
                            await Task.Delay((loopTimeInt > 0) ? loopTimeInt * 1000 : 0);
                    } while (runInLoop.IsChecked ?? false);

                    running.Text = "Stopped";
                    await Task.Delay(2000);
                    running.Visibility = Visibility.Hidden;
                    SearchingBorder.Visibility = Visibility.Hidden;

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                Run.IsEnabled = true;
            }
            else
            {
                MessageBox.Show("Can't run with an empty link");
            }
        }


        // Makes DataGrid Scrollable
        private void UIElement_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
            e.Handled = true;
        }


        // Updating DataGrid Context
        private void FrameworkElement_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DataContext = e.NewValue;
        }




        private void monthBtnClicked(object sender, RoutedEventArgs e)
        {

        }

        private void notifyMeBtnClicked(object sender, RoutedEventArgs e)
        {
            DataContext = _notifyMe;

            contentControl.Visibility = Visibility.Visible;
            notifyMeUC.mailingListBlock.Visibility = Visibility.Visible;
            table1.Visibility = Visibility.Hidden;
            table2.Visibility = Visibility.Hidden;
            searchBox.Visibility = Visibility.Hidden;
            inStockLabel.Visibility = Visibility.Hidden;
            outOfStockLabel.Visibility = Visibility.Hidden;
            SearchFiltersScroller.Visibility = Visibility.Hidden;
            SearchFiltersHeader.Visibility = Visibility.Hidden;
            SearchCategoryPanel.Visibility = Visibility.Hidden;
        }

        private void debugBtnClicked(object sender, RoutedEventArgs e)
        {

        }

        private void homeClicked(object sender, RoutedEventArgs e)
        {
            //notifyMeUC.mailingListBlock.Text = mailListt;
            contentControl.Visibility = Visibility.Hidden;
            DataContext = scraper;

            inStockLabel.Visibility = Visibility.Visible;
            outOfStockLabel.Visibility = Visibility.Visible;
            table1.Visibility = Visibility.Visible;
            table2.Visibility = Visibility.Visible;
            searchBox.Visibility = Visibility.Visible;

            if (SearchFilters.Children.Count != 0)
            {
                SearchFiltersScroller.Visibility = Visibility.Visible;
                SearchCategoryPanel.Visibility = Visibility.Visible;
                SearchFiltersHeader.Visibility = Visibility.Visible;
            }



            if (NotifyMe.Notify && string.IsNullOrEmpty(NotifyMe.MailingList))
                MessageBox.Show("cannot send an email To an empty mailing list");

        }

        private void formLoad(object sender, RoutedEventArgs e)
        {
            if (!runInLoop.IsChecked ?? false)
            {
                loopTime.IsEnabled = false;
                loopTime.Opacity = 0.5;
            }

            contentControl.Visibility = Visibility.Hidden;
            running.Visibility = Visibility.Hidden;
            SearchingBorder.Visibility = Visibility.Hidden;
            SearchCategoryPanel.Visibility = Visibility.Hidden;
            SearchFiltersHeader.Visibility = Visibility.Hidden;
            SearchFiltersScroller.Visibility = Visibility.Hidden;
        }

        private void loopChecked(object sender, RoutedEventArgs e)
        {
            loopTime.IsEnabled = true;
            loopTime.Opacity = 100;
        }

        private void loopUnchecked(object sender, RoutedEventArgs e)
        {
            loopTime.IsEnabled = false;
            Run.IsEnabled = true;
            loopTime.Opacity = 0.7;
        }

        private void loopTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int loop = IsAnumber(loopTime.Text) ? Int32.Parse(loopTime.Text) : 0;
            loopTimeInt = loop;
        }

        public bool IsAnumber(string num)
        {
            int n;
            bool isAnumber = Int32.TryParse(num, out n);


            if (!isAnumber && runInLoop.IsChecked == true)
                MessageBox.Show("Please provide a valid number for the loop");

            return isAnumber;
        }

        private void formMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton != MouseButtonState.Pressed)
                this.DragMove();
        }

        private void closeBtn(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void miniBtn(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void maxiBtn(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
        }
    }
}
