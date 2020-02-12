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

namespace Handwriting
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        Bitmap bBlank, bDisplay, bitmap, blackBrush, bPaper, brush, bText;
        bool isSelecting = false, isWriting = false;
        Button bColor;
        Canvas canvas, cBlank, cDisplay, cText;
        Color brushColor = Color.Black;
        float BOTTOM, left, right, TOP;
        float brushWidth, prevX, prevY, size, strokeWidth = 48;
        float column = 0, line = 0;
        ImageView ivCanvas;
        int charHeight = 0x80, HEIGHT, padding = 8, WIDTH;
        readonly Paint paint = new Paint() { StrokeWidth = 2 };

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            bColor = FindViewById<Button>(Resource.Id.b_color);
            ivCanvas = FindViewById<ImageView>(Resource.Id.iv_canvas);

            FindViewById<Button>(Resource.Id.b_backspace).Click += BBackspace_Click;
            bColor.Click += BColor_Click;
            FindViewById<Button>(Resource.Id.b_next).Click += BNext_Click;
            FindViewById<Button>(Resource.Id.b_paper).Click += BPaper_Click;
            FindViewById<Button>(Resource.Id.b_return).Click += BReturn_Click;
            FindViewById<Button>(Resource.Id.b_space).Click += BSpace_Click;
            ivCanvas.Touch += IvCanvas_Touch;
            FindViewById<SeekBar>(Resource.Id.sb_height).ProgressChanged += SbHeight_ProgressChanged; ;
            FindViewById<SeekBar>(Resource.Id.sb_width).ProgressChanged += SbWidth_ProgressChanged;

            blackBrush = BitmapFactory.DecodeResource(Resources, Resource.Mipmap.brush);
            brush = blackBrush.Copy(Bitmap.Config.Argb8888, true);
        }

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
                SetBrushColor(Color.Red);
                bColor.SetTextColor(Color.Red);
            }
            else
            {
                SetBrushColor(Color.Black);
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
            line += charHeight;
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
                    else if (x > right)
                        right = (int)x;
                    float d = (float)Math.Sqrt(Math.Pow(x - prevX, 2) + Math.Pow(y - prevY, 2)),
                        a = d / (float)Math.Pow(d, 2),
                        w = 0,
                        width = (float)Math.Pow(1 - d / size, 6) * strokeWidth,
                        xpd = (x - prevX) / d, ypd = (y - prevY) / d;
                    if (width >= brushWidth)
                    {
                        for (float f = 0; f < d; f += 4)
                        {
                            w = a * (float)Math.Pow(f, 2) / d * (width - brushWidth) + brushWidth;
                            canvas.DrawBitmap(brush, new Rect(0, 0, 192, 192), new Rect((int)(xpd * f + prevX - w), (int)(ypd * f + prevY - w), (int)(xpd * f + prevX + w), (int)(ypd * f + prevY + w)), paint);
                            cBlank.DrawBitmap(brush, new Rect(0, 0, 192, 192), new Rect((int)(xpd * f + prevX - w), (int)(ypd * f + prevY - w), (int)(xpd * f + prevX + w), (int)(ypd * f + prevY + w)), paint);
                        }
                    }
                    else
                    {
                        for (float f = 0; f < d; f += 4)
                        {
                            w = (float)Math.Sqrt(f / a) / d * (width - brushWidth) + brushWidth;
                            canvas.DrawBitmap(brush, new Rect(0, 0, 192, 192), new Rect((int)(xpd * f + prevX - w), (int)(ypd * f + prevY - w), (int)(xpd * f + prevX + w), (int)(ypd * f + prevY + w)), paint);
                            cBlank.DrawBitmap(brush, new Rect(0, 0, 192, 192), new Rect((int)(xpd * f + prevX - w), (int)(ypd * f + prevY - w), (int)(xpd * f + prevX + w), (int)(ypd * f + prevY + w)), paint);
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

        void Load()
        {
            WIDTH = ivCanvas.Width;
            HEIGHT = ivCanvas.Height;
            bitmap = Bitmap.CreateBitmap(WIDTH, HEIGHT, Bitmap.Config.Argb8888);
            canvas = new Canvas(bitmap);
            bBlank = Bitmap.CreateBitmap(bitmap);
            cBlank = new Canvas(bBlank);
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
                    Bitmap bChar = Bitmap.CreateBitmap(bitmap, (int)left, 0, (int)(right - left), HEIGHT);
                    int charWidth = (int)((float)charHeight / WIDTH * (right - left));
                    bChar = Bitmap.CreateScaledBitmap(bChar, charWidth, (int)((float)charHeight / WIDTH * HEIGHT), true);
                    column += padding;
                    if (column + charWidth > WIDTH)
                    {
                        column = 0;
                        line += charHeight;
                    }
                    cText.DrawBitmap(bChar, column, line - (float)charHeight / WIDTH * TOP, paint);
                    bChar.Dispose();
                    column += charWidth;
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
                    bPaper = Bitmap.CreateScaledBitmap(bPaper, cText.Width, cText.Height, true);
                    cText.DrawBitmap(bPaper, 0, 0, paint);
                    ivCanvas.SetImageBitmap(bText);
                    SetCursor();
                    fs.Flush();
                    fs.Dispose();
                }
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private void SbHeight_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            if (bitmap == null)
                Load();
            charHeight = e.Progress;
            Bitmap bDisplay = Bitmap.CreateBitmap(bText);
            Canvas cDisplay = new Canvas(bDisplay);
            cDisplay.DrawLine(WIDTH / 2, HEIGHT / 2, WIDTH / 2, HEIGHT / 2 + charHeight, paint);
            ivCanvas.SetImageBitmap(bDisplay);
            cDisplay.Dispose();
            bDisplay.Dispose();
        }

        private void SbWidth_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            strokeWidth = e.Progress;
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
        }

        void SetCursor(bool style = false)
        {
            paint.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.Clear));
            cDisplay.DrawPaint(paint);
            paint.SetXfermode(null);
            cDisplay.DrawBitmap(bText, 0, 0, paint);
            Paint p = new Paint() { StrokeWidth = 8 };
            p.SetStyle(Paint.Style.Stroke);
            cDisplay.DrawRect(column, style ? line : line + charHeight, column + charHeight / 2, line + charHeight, p);
            ivCanvas.SetImageBitmap(bDisplay);
        }
    }
}