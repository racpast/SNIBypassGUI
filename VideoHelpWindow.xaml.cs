﻿using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SNIBypassGUI
{
    /// <summary>
    /// VideoHelpWindow.xaml 的交互逻辑
    /// </summary>
    public partial class VideoHelpWindow : Window
    {
        private string _tip, _videopath;

        public VideoHelpWindow(string tip,string videopath)
        {
            _tip = tip;
            _videopath = videopath;
            InitializeComponent();
            // 窗口可拖动化
            this.TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Title = _tip;
            WindowTitle.Text = _tip;
            videoView.Source = new Uri(_videopath);
            videoView.Play();
        }

        private void OKBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void videoView_MediaEnded(object sender, RoutedEventArgs e)
        {
            // 循环播放视频
            videoView.Stop();
            videoView.Play();
        }

        private void videoView_Loaded(object sender, RoutedEventArgs e)
        {
            videoView.Play();
        }

        private void videoView_Unloaded(object sender, RoutedEventArgs e)
        {
            videoView.Stop();
        }

        private void videoView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 判断当前媒体状态来决定暂停还是播放
            if (GetMediaState(videoView) == MediaState.Play)
            {
                videoView.Pause();
            }
            else
            {
                videoView.Play();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            videoView.Stop();
        }

        // 通过反射获取 MediaElement 控件的当前媒体状态
        private MediaState GetMediaState(MediaElement myMedia)
        {
            FieldInfo hlp = typeof(MediaElement).GetField("_helper", BindingFlags.NonPublic | BindingFlags.Instance);
            object helperObject = hlp.GetValue(myMedia);
            FieldInfo stateField = helperObject.GetType().GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance);
            MediaState state = (MediaState)stateField.GetValue(helperObject);
            return state;
        }
    }
}
