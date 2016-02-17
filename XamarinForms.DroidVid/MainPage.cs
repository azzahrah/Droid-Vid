﻿using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace DroidVid.XamarinForms
{
    public class MainPage : ContentPage
    {
        VideoView vv;
        Button btn;
        Rectangle leftPlace, rightPlace;
        RelativeLayout rel;
        double rLocX, rLocY, lLocX, lLocY;
        double rWidth, rHeight, lWidth, lHeight;

        public MainPage()
        {
            rLocX = .45;
            rLocY = .01;
            lLocX = 0.15;
            lLocY = 0.25;

            rWidth = rHeight  = 1.0 / 2;

            lHeight = lWidth = 1.0 / 5;

            rel = new RelativeLayout();

            rel.Children.Add(vv = new VideoView(),
                Constraint.RelativeToParent ((parent) => { return parent.Width * rLocX; }),
                Constraint.RelativeToParent ((parent) => { return parent.Height * rLocY; }),
                Constraint.RelativeToParent ((parent) => { return parent.Width * rWidth; }),
                Constraint.RelativeToParent ((parent) => { return parent.Height * rHeight; }));

            btn = new Button() { Text = "button" };

            btn.Clicked += (object sender, EventArgs e) => MoveViews();

            rel.Children.Add(btn,
                Constraint.RelativeToParent ((parent) => { return parent.Width * lLocX; }),
                Constraint.RelativeToParent ((parent) => { return parent.Height * lLocY; }),
                Constraint.RelativeToParent ((parent) => { return parent.Width * lWidth; }),
                Constraint.RelativeToParent ((parent) => { return parent.Height * lHeight; }));

            //add everything in the relative layou to the page
            Content = rel;

            //Tick();

        }

        private async void Tick()
        {
            await Task.Delay(1000).ConfigureAwait(false);//tick @ 1Hz
            ++count;

            //this is will allow the layout to change, but will stop updates.
            //if(swap)
                Device.BeginInvokeOnMainThread(() => UpdateText());


            Task.Run(() => Tick());
        }

        bool swap = false;
        volatile int count = 0;
        System.Threading.ManualResetEvent locker = new System.Threading.ManualResetEvent(false);

        Task<bool> lastMove;


        private async void MoveViews()
        {
            //++count;

            double w = rel.Width;
            double h = rel.Height;

            if (rightPlace.Height == 0)
                rightPlace = new Rectangle(w*rLocX,h*rLocY,w*rWidth,h*rHeight);

            if (leftPlace.Height == 0)
                leftPlace = new Rectangle(w*lLocX,h*lLocY,w*lWidth,h*lHeight);

            Rectangle videoPlace, btnPlace;

            if (swap)
            {
                videoPlace = leftPlace;
                btnPlace = rightPlace;

                //vv.RelScaleTo(-0.25);
            }
            else
            {
                //vv.RelScaleTo(0.25);
                videoPlace = rightPlace;
                btnPlace = leftPlace;
            }

            UpdateText();

            //move the video view
            //lock (locker)
            {
                //vv.TranslateTo(videoPlace.X,
                //                videoPlace.Y);

                //lastMove = btn.TranslateTo(btnPlace.X,
                //                            btnPlace.Y);

                vv.LayoutTo(videoPlace);

                btn.LayoutTo(btnPlace);
            }

            swap = !swap;
        }

        bool UpdateText()
        {
            btn.Text = count+" s";

            return true;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
        }
    }
}