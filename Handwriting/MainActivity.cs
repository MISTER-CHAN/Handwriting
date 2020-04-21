using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using Android.Views;
using System;
using Android.Graphics;
using Android.Content;
using Android.Provider;
using Android.Database;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace Handwriting
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        Bitmap bBlank, bChar, bDisplay, bitmap, blackBrush, bPaper, bPreview, bPreviewPaper, brush, bText, redBrush;
        bool autoNewline = false, backspace = false, isSelecting = false, isWriting = false, space = false;
        Button bBackspace, bColor, bNext, bReturn, bSpace;
        Canvas canvas, cBlank, cChar, cDisplay, cPreview, cText;
        Color brushColor = Color.Black;
        float BOTTOM, left, right, TOP;
        float alias = 8, brushWidth, prevX, prevY, previewX = 0, previewY = 0, ratio = 2f, size, strokeWidth = 72;
        float column = 0, line = 0;
        ImageView ivCanvas, ivPreview;
        int charHeight = 64, charWidth = -1, handwriting = 6, HEIGHT, horizontalGap = 4, verticalGap = 0, WIDTH;
        LinearLayout llOptions;
        readonly Paint paint = new Paint() { StrokeWidth = 2 };
        SeekBar sbCharWidth;

        private void BBackspace_Click(object sender, EventArgs e)
        {
            if (bitmap == null)
                Load();
            if (isWriting)
            {
                isWriting = false;
                paint.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.Clear));
                canvas.DrawPaint(paint);
                paint.SetXfermode(null);
                ivCanvas.SetImageBitmap(bDisplay);
            }
            else
            {
                if (column > 0)
                    column -= charHeight / 4;
                else
                {
                    line -= charHeight;
                    column = WIDTH - charHeight / 4; ;
                }
                if (bPaper == null)
                {
                    cText.DrawRect(column, line, column + charHeight / 4, line + charHeight, new Paint() { Color = Color.White });
                }
                else if (0 <= column && column < WIDTH)
                {
                    Bitmap b = Bitmap.CreateBitmap(bPaper, (int)column, (int)line, charHeight / 4, charHeight);
                    cText.DrawBitmap(b, column, line, paint);
                    b.Dispose();
                }
                ivCanvas.SetImageBitmap(bText);
                SetCursor();
            }
        }

        private void BColor_Click(object sender, EventArgs e)
        {
            if (brushColor == Color.Black)
            {
                brush = redBrush.Copy(Bitmap.Config.Argb8888, true);
                brushColor = Color.Red;
                bColor.SetTextColor(Color.Red);
            }
            else
            {
                brush = blackBrush.Copy(Bitmap.Config.Argb8888, true);
                brushColor = Color.Black;
                bColor.SetTextColor(Color.Black);
            }
        }

        private void BNext_Click(object sender, System.EventArgs e)
        {
            if (isWriting)
            {
                Next();
            }
            else
            {
                isSelecting = !isSelecting;
            }
        }

        private void BOptions_Click(object sender, EventArgs e)
        {
            if (llOptions.Visibility == ViewStates.Gone)
            {
                ivCanvas.Visibility = ViewStates.Gone;
                bSpace.Visibility = ViewStates.Invisible;
                bNext.Visibility = ViewStates.Invisible;
                bBackspace.Visibility = ViewStates.Invisible;
                bReturn.Visibility = ViewStates.Invisible;
                llOptions.Visibility = ViewStates.Visible;
                if (bPreviewPaper == null)
                    bPreviewPaper = Bitmap.CreateBitmap(ivCanvas.Width, ivCanvas.Height, Bitmap.Config.Argb8888);
            }
            else
            {
                llOptions.Visibility = ViewStates.Gone;
                bSpace.Visibility = ViewStates.Visible;
                bNext.Visibility = ViewStates.Visible;
                bBackspace.Visibility = ViewStates.Visible;
                bReturn.Visibility = ViewStates.Visible;
                ivCanvas.Visibility = ViewStates.Visible;
            }
        }

        private void BPaper_Click(object sender, EventArgs e)
        {
            if (bitmap == null)
                Load();
            Intent intent = new Intent(Intent.ActionPick, null);
            intent.SetType("image/*");// 设置文件类型
            StartActivityForResult(intent, 2); //2代表看相册
        }

        private void BReturn_Click(object sender, EventArgs e)
        {
            if (bitmap == null)
                Load();
            if (isWriting)
                Next();
            column = 0;
            line += charHeight + verticalGap;
            SetCursor();
        }

        private void BSpace_Click(object sender, EventArgs e)
        {
            if (bitmap == null)
                Load();
            if (isWriting)
                Next();
            column += charHeight / 4;
            SetCursor();
        }
        public static string GetDataColumn(Context context, Android.Net.Uri uri, string selection, string[] selectionArgs)
        {

            ICursor cursor = null;
            string column = "_data";
            string[] projection = { column };

            try
            {
                cursor = context.ContentResolver.Query(uri, projection, selection, selectionArgs,
                    null);
                if (cursor != null && cursor.MoveToFirst())
                {
                    int index = cursor.GetColumnIndexOrThrow(column);
                    return cursor.GetString(index);
                }
            }
            finally
            {
                if (cursor != null)
                    cursor.Close();
            }
            return null;
        }

        private void IvCanvas_Touch(object sender, View.TouchEventArgs e)
        {
            if (bitmap == null)
                Load();
            float x = e.Event.GetX(), y = e.Event.GetY();
            if (isSelecting)
            {
                column = x - charHeight / 8;
                line = y - charHeight / 2;
                SetCursor(true);
                return;
            }
            switch (e.Event.Action)
            {
                case MotionEventActions.Down:
                    prevX = x;
                    prevY = y;
                    brushWidth = 0;
                    if (!isWriting)
                    {
                        isWriting = true;
                        paint.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.Clear));
                        cBlank.DrawPaint(paint);
                        paint.SetXfermode(null);
                        cBlank.DrawBitmap(bDisplay, 0, 0, paint);
                        cBlank.DrawColor(new Color(0xff, 0xff, 0xff, 0x7f));
                        cBlank.DrawLine(0, TOP, ivCanvas.Width, TOP, paint);
                        cBlank.DrawLine(0, BOTTOM, ivCanvas.Width, BOTTOM, paint);
                    }
                    break;
                case MotionEventActions.Move:
                    if (x < left)
                        left = (int)x;
                    if (x > right)
                        right = (int)x;
                    float d = (float)Math.Sqrt(Math.Pow(x - prevX, 2) + Math.Pow(y - prevY, 2)),
                        a = d / (float)Math.Pow(d, ratio),
                        w = 0,
                        width = (float)Math.Pow(1 - d / size, handwriting) * strokeWidth,
                        xpd = (x - prevX) / d, ypd = (y - prevY) / d;
                    if (width >= brushWidth)
                    {
                        for (float f = 0; f < d; f += alias)
                        {
                            w = a * (float)Math.Pow(f, ratio) / d * (width - brushWidth) + brushWidth;
                            Rect r = new Rect((int)(xpd * f + prevX - w), (int)(ypd * f + prevY - w), (int)(xpd * f + prevX + w), (int)(ypd * f + prevY + w));
                            canvas.DrawBitmap(brush, new Rect(0, 0, 192, 192), r, paint);
                            cBlank.DrawBitmap(brush, new Rect(0, 0, 192, 192), r, paint);
                        }
                    }
                    else
                    {
                        for (float f = 0; f < d; f += alias)
                        {
                            w = (float)Math.Pow(f / a, 1 / ratio) / d * (width - brushWidth) + brushWidth;
                            Rect r = new Rect((int)(xpd * f + prevX - w), (int)(ypd * f + prevY - w), (int)(xpd * f + prevX + w), (int)(ypd * f + prevY + w));
                            canvas.DrawBitmap(brush, new Rect(0, 0, 192, 192), r, paint);
                            cBlank.DrawBitmap(brush, new Rect(0, 0, 192, 192), r, paint);
                        }
                    }
                    brushWidth = w;
                    prevX = x;
                    prevY = y;
                    break;
                case MotionEventActions.Up:
                    return;
            }
            ivCanvas.SetImageBitmap(bBlank);
        }

        private void IvPreview_Touch(object sender, View.TouchEventArgs e)
        {
            previewX = e.Event.GetX();
            previewY = e.Event.GetY();
            Preview();
        }

        void Load()
        {
            WIDTH = ivCanvas.Width;
            HEIGHT = ivCanvas.Height;
            bitmap = Bitmap.CreateBitmap(WIDTH, HEIGHT, Bitmap.Config.Argb8888);
            canvas = new Canvas(bitmap);
            bBlank = Bitmap.CreateBitmap(bitmap);
            cBlank = new Canvas(bBlank);
            bChar = Bitmap.CreateBitmap(WIDTH, HEIGHT, Bitmap.Config.Argb8888);
            cChar = new Canvas(bChar);
            bText = Bitmap.CreateBitmap(WIDTH, HEIGHT, Bitmap.Config.Argb8888);
            cText = new Canvas(bText);
            bDisplay = Bitmap.CreateBitmap(bText);
            cDisplay = new Canvas(bDisplay);
            ivCanvas.SetImageBitmap(bBlank);
            size = (float)Math.Sqrt(Math.Pow(WIDTH, 2) + Math.Pow(HEIGHT, 2));
            left = WIDTH;
            right = 0;
            TOP = (HEIGHT - WIDTH) / 2;
            BOTTOM = TOP + WIDTH;
            SetCursor();
        }

        void Next()
        {
            isWriting = false;
            if (right > left)
            {
                try
                {
                    paint.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.Clear));
                    cChar.DrawPaint(paint);
                    paint.SetXfermode(null);
                    int charWidth = (int)((float)charHeight / WIDTH * (right - left));
                    cChar.DrawBitmap(bitmap, new Rect((int)left, 0, (int)right, HEIGHT), new Rect(0, 0, charWidth, (int)((float)charHeight / WIDTH * HEIGHT)), paint);
                    column += horizontalGap;
                    if (autoNewline && column + charWidth > WIDTH)
                    {
                        line += charHeight + verticalGap;
                        column = 0;
                    }
                    cText.DrawBitmap(bChar, column, line - (float)charHeight / WIDTH * TOP, paint);
                    column += this.charWidth == -1 ? charWidth: this.charWidth;
                    if (!autoNewline && column > WIDTH)
                    {
                        line += charHeight + verticalGap;
                        column = 0;
                    }
                }
                catch
                {
                    Toast.MakeText(this, "Error!", ToastLength.Long).Show();
                }
            }
            paint.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.Clear));
            canvas.DrawPaint(paint);
            paint.SetXfermode(null);
            SetCursor();
            left = ivCanvas.Width;
            right = 0;
        }
        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result ResultStatus, Intent data)
        {
            if (ResultStatus != Result.Ok)
            {
                return;
            }

            if (requestCode == 2)
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
                {
                    var url = Android.Net.Uri.Parse("file://" + GetDataColumn(BaseContext, data.Data, null, null));

                    data.SetData(url);
                    FileStream fs = File.Open(url.Path, FileMode.Open, FileAccess.Read);
                    bPaper = BitmapFactory.DecodeStream(fs);
                    fs.Flush();
                    fs.Dispose();
                    bPaper = Bitmap.CreateScaledBitmap(bPaper, cText.Width, cText.Height, true);
                    cText.DrawBitmap(bPaper, 0, 0, paint);
                    ivCanvas.SetImageBitmap(bText);
                    new Canvas(bPreviewPaper).DrawBitmap(bPaper, 0, 0, paint);
                    Preview();
                    SetCursor();
                }
            }
        }

        public override void OnBackPressed()
        {
            MoveTaskToBack(true);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            bBackspace = FindViewById<Button>(Resource.Id.b_backspace);
            bColor = FindViewById<Button>(Resource.Id.b_color);
            bNext = FindViewById<Button>(Resource.Id.b_next);
            bReturn = FindViewById<Button>(Resource.Id.b_return);
            bSpace = FindViewById<Button>(Resource.Id.b_space);
            ivCanvas = FindViewById<ImageView>(Resource.Id.iv_canvas);
            ivPreview = FindViewById<ImageView>(Resource.Id.iv_preview);
            llOptions = FindViewById<LinearLayout>(Resource.Id.ll_options);
            sbCharWidth = FindViewById<SeekBar>(Resource.Id.sb_char_width);

            bBackspace.Click += BBackspace_Click;
            bColor.Click += BColor_Click;
            bNext.Click += BNext_Click;
            FindViewById<Button>(Resource.Id.b_options).Click += BOptions_Click;
            FindViewById<Button>(Resource.Id.b_paper).Click += BPaper_Click;
            bReturn.Click += BReturn_Click;
            bSpace.Click += BSpace_Click;
            ivCanvas.Touch += IvCanvas_Touch;
            ivPreview.Touch += IvPreview_Touch;
            FindViewById<RadioButton>(Resource.Id.rb_char_width_auto).CheckedChange += RbCharWidthAuto_CheckedChange;
            FindViewById<RadioButton>(Resource.Id.rb_char_width_custom).CheckedChange += RbCharWidthCustom_CheckedChange;
            FindViewById<Switch>(Resource.Id.s_newline).CheckedChange += SNewline_CheckedChange;
            FindViewById<SeekBar>(Resource.Id.sb_alias).ProgressChanged += SbAlias_ProgressChanged;
            FindViewById<SeekBar>(Resource.Id.sb_char_height).ProgressChanged += SbCharHeight_ProgressChanged;
            sbCharWidth.ProgressChanged += SbCharWidth_ProgressChanged;
            FindViewById<SeekBar>(Resource.Id.sb_handwriting).ProgressChanged += SbHandwriting_ProgressChanged;
            FindViewById<SeekBar>(Resource.Id.sb_horizontal_gap).ProgressChanged += SbHorizontalGap_ProgressChanged;
            FindViewById<SeekBar>(Resource.Id.sb_ratio).ProgressChanged += SbRatio_ProgressChanged;
            FindViewById<SeekBar>(Resource.Id.sb_stroke_width).ProgressChanged += SbStrokeWidth_ProgressChanged;
            FindViewById<SeekBar>(Resource.Id.sb_vertical_gap).ProgressChanged += SbVerticalGap_ProgressChanged;

            redBrush = BitmapFactory.DecodeResource(Resources, Resource.Mipmap.brush_red);
            blackBrush = BitmapFactory.DecodeResource(Resources, Resource.Mipmap.brush);
            brush = blackBrush.Copy(Bitmap.Config.Argb8888, true);

        }

        private void SbAlias_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            alias = e.Progress;
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        void Preview()
        {
            if (bPreview == null)
            {
                bPreview = Bitmap.CreateBitmap(ivPreview.Width, ivPreview.Height, Bitmap.Config.Argb8888);
                cPreview = new Canvas(bPreview);
                previewX = ivPreview.Width / 2; previewY = ivPreview.Height / 2;
            }
            Paint p = new Paint() { StrokeWidth = 2 };
            p.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.Clear));
            cPreview.DrawPaint(p);
            p.SetXfermode(null);
            p.SetStyle(Paint.Style.Stroke);
            cPreview.DrawBitmap(bPreviewPaper, 0, 0, p);
            cPreview.DrawRect(previewX, previewY, previewX + (charWidth == -1 ? charHeight : charWidth), previewY + charHeight, p);
            cPreview.DrawRect(previewX - horizontalGap, previewY - verticalGap, previewX + (charWidth == -1 ? charHeight : charWidth) + horizontalGap, previewY + charHeight + verticalGap, p);
            ivPreview.SetImageBitmap(bPreview);
        }

        private void RbCharWidthAuto_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if (e.IsChecked)
            {
                sbCharWidth.Visibility = ViewStates.Gone;
                charWidth = -1;
                Preview();
            }
        }

        private void RbCharWidthCustom_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if (e.IsChecked)
            {
                sbCharWidth.Visibility = ViewStates.Visible;
                charWidth = sbCharWidth.Progress;
                Preview();
            }
        }

        private void SNewline_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            autoNewline = e.IsChecked;
        }

        private void SbCharHeight_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            charHeight = e.Progress;
            Preview();
        }

        private void SbCharWidth_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            charWidth = e.Progress;
            Preview();
        }

        private void SbHandwriting_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            handwriting = e.Progress;
        }

        private void SbHorizontalGap_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            horizontalGap = e.Progress;
            Preview();
        }

        private void SbRatio_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            ratio = e.Progress / 10f;
        }

        private void SbStrokeWidth_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            strokeWidth = e.Progress;
        }

        private void SbVerticalGap_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            verticalGap = e.Progress;
            Preview();
        }
        void SetBrushColor(Color color)
        {
            if (color == brushColor)
            {
                return;
            }
            int brushHeight = blackBrush.Height, brushWidth = blackBrush.Width;
            for (int i = 0; i < brushWidth; i++)
            {
                for (int j = 0; j < brushHeight; j++)
                {
                    if (blackBrush.GetPixel(i, j) != Color.Transparent)
                    {
                        brush.SetPixel(i, j, color);
                    }

                }
            }
            brushColor = color;
            bColor.SetTextColor(color);
        }

        void SetCursor(bool style = false)
        {
            Paint p = new Paint() { StrokeWidth = 8 };
            p.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.Clear));
            cDisplay.DrawPaint(p);
            p.SetXfermode(null);
            cDisplay.DrawBitmap(bText, 0, 0, p);
            p.SetStyle(Paint.Style.Stroke);
            cDisplay.DrawRect(column, style ? line : line + charHeight, column + (charWidth == -1 ? charHeight : charWidth), line + charHeight, p);
            ivCanvas.SetImageBitmap(bDisplay);
        }
    }
}