using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Drawing.Imaging;
using System.Xml;

namespace paint
{
    public partial class MainWindow : Window
    {
        struct Types
        {//тип штриха
            public string Stype { get; set; }
            public DoubleCollection Dtype { get; set; }
        };
        public MainWindow()
        {
            InitializeComponent();
            List<int> strokeSizes = new List<int>() { 1, 2, 3, 4 };
            cbSizeStroke.ItemsSource = strokeSizes;
            List<Types> strokeTypes = new List<Types>();//тип штриха
            strokeTypes.Add(new Types() {Stype="",Dtype=new DoubleCollection() });
            strokeTypes.Add(new Types() { Stype = "1", Dtype = new DoubleCollection(new double[] { 1}) });
            strokeTypes.Add(new Types() { Stype = "4 1 1 1", Dtype = new DoubleCollection(new double[] { 4,1,1,1 }) });
            strokeTypes.Add(new Types() { Stype = "4 1 1 1 1 1", Dtype = new DoubleCollection(new double[] { 4, 1, 1, 1 ,1,1}) });
            cbTypeStroke.ItemsSource = strokeTypes;
            List<string> cols = new List<string>() { "Black" , "LightSlateGray" , "DarkRed","Red","DarkOrange","Yellow",
                "Green","DeepSkyBlue","RoyalBlue","MediumOrchid","White","LightGray","RosyBrown","Pink","Orange","Beige",
                "YellowGreen","PaleTurquoise","SteelBlue","Lavender"};
            lbColors.ItemsSource = cols;
            paintSurface.MouseMove += Cursor_MouseMove;
        }

        Point currentPoint = new Point();//текущее положение для рисования
        private Point LastPoint;//последнее положение изменения
        Object currObj = new Object();//текущий объект
        Object selectedObj = new Object();//выделенный обЪект
        Object copyObj = new Object();//скопированный объект

        private enum HitType
        {//положение мыши на обЪекте
            None, Body, UL, UR, LR, LL, L, R, T, B
        };

        HitType MouseHitType = HitType.None;

        private bool isDragInProgress = false;//осуществляется ли перемещение/изменение
       
        private void CreateNewFile(object sender, RoutedEventArgs e)
        {//кнопка Создать файл
            Ask ask = new Ask();
            if (ask.ShowDialog() == true)
            {
                if (ask.choice) SaveFile(sender, e);
                Clear(sender, e);
            }
        }
        private void SaveFile(object sender, RoutedEventArgs e)
        {//сохранить файл
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Изображение|*.png;|Данные|*.txt;";
            if (sfd.ShowDialog() == true)
            {
                string ext = System.IO.Path.GetExtension(sfd.FileName);
                switch (ext)
                {
                    case ".png":
                        Rect bounds = VisualTreeHelper.GetDescendantBounds(paintSurface);
                        double dpi = 96d;
                        RenderTargetBitmap rtb = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, dpi, dpi, System.Windows.Media.PixelFormats.Default);

                        DrawingVisual dv = new DrawingVisual();
                        using (DrawingContext dc = dv.RenderOpen())
                        {
                            VisualBrush vb = new VisualBrush(paintSurface);
                            dc.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
                        }
                        rtb.Render(dv);
                        BitmapEncoder pngEncoder = new PngBitmapEncoder();
                        pngEncoder.Frames.Add(BitmapFrame.Create(rtb));

                        System.IO.MemoryStream ms = new System.IO.MemoryStream();

                        pngEncoder.Save(ms);
                        ms.Close();
                        System.IO.File.WriteAllBytes(sfd.FileName, ms.ToArray());
                        break;
                    case ".txt":
                        System.IO.FileStream fs = new System.IO.FileStream(sfd.FileName, System.IO.FileMode.Create);
                        System.IO.StreamWriter sw = new System.IO.StreamWriter(fs);
                        foreach (UIElement el in paintSurface.Children)
                        {
                            string s = XamlWriter.Save(el);
                            sw.WriteLine(s);
                        }
                        sw.Close();
                        break;
                }
            }
        }
        private void OpenFile(object sender, RoutedEventArgs e)
        {//открыть файл
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Изображение|*.png;|Данные|*.txt;";
            if (ofd.ShowDialog() == true)
            {
                string ext = System.IO.Path.GetExtension(ofd.FileName);
                switch (ext)
                {
                    case ".txt":
                        System.IO.FileStream fs = new System.IO.FileStream(ofd.FileName, System.IO.FileMode.Open);
                        System.IO.StreamReader sr = new System.IO.StreamReader(fs);
                        string s;
                        while ((s = sr.ReadLine()) != null)
                        {
                            System.IO.StringReader stringReader = new System.IO.StringReader(s);
                            XmlReader xmlReader = XmlReader.Create(stringReader);
                            UIElement sh = (UIElement)XamlReader.Load(xmlReader);
                            paintSurface.Children.Add(sh);
                            AddRightMouseClickEvent(sh);
                        }
                        sr.Close();
                        foreach (UIElement el in paintSurface.Children)
                            AddRightMouseClickEvent(el);
                        break;
                    case ".png":
                        Image img = new Image();
                        img.Source = new BitmapImage(new Uri(ofd.FileName));
                        paintSurface.Children.Add(img);
                        Canvas.SetTop(img, 0);
                        Canvas.SetLeft(img, 0);
                        img.Loaded += delegate
                        {
                            paintSurface.Width = img.ActualWidth;
                            paintSurface.Height = img.ActualHeight;
                            if (paintSurface.Width >= myGrid.ActualWidth)
                                myGrid.Width = paintSurface.Width + 10;
                            if (paintSurface.Height >= myGrid.ActualHeight)
                                myGrid.Height = paintSurface.Height + 10;
                        };
                        break;
                }
            }
        }
        private void PasteItem(object sender, RoutedEventArgs e)
        {//кнопка вставки
            UIElement newEl = XamlReader.Parse(XamlWriter.Save(copyObj)) as UIElement;
            AddRightMouseClickEvent(newEl);
            paintSurface.Children.Add(newEl);
        }
        private void Clear(object sender, RoutedEventArgs e)
        {//очистить экран
            paintSurface.Children.Clear();
            Thumb myTh = myWin.FindResource("myThumb") as Thumb;
            paintSurface.Children.Add(myTh);
        }
        private void btnDraw_OnClick(object sender, RoutedEventArgs e)
        {//кнопка рисования
            paintSurface.MouseMove -= Line_MouseMove;
            paintSurface.MouseMove -= Shapes_MouseMove;
            paintSurface.MouseMove -= Draw_MouseMove;
            paintSurface.MouseMove += Draw_MouseMove;
            Shape shap = selectedObj as Shape;
            if (shap != null)
                shap.Opacity = 1;
            selectedObj = null;
        }
        private void Draw_MouseMove(object sender, MouseEventArgs e)
        {//рисование кривой
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Line line = new Line();
                line.Stroke = strokeColor.Fill;
                line.StrokeThickness = cbSizeStroke.SelectedIndex + 1;
                line.X1 = currentPoint.X;
                line.Y1 = currentPoint.Y - stPanel.ActualHeight;
                line.X2 = e.GetPosition(this).X;
                line.Y2 = e.GetPosition(this).Y - stPanel.ActualHeight;
                currentPoint = e.GetPosition(this);
                paintSurface.Children.Add(line);
            }
        }
        private void btnCursor_OnClick(object sender, RoutedEventArgs e)
        {//кнопка курсора
            paintSurface.MouseMove -= Draw_MouseMove;
            paintSurface.MouseMove -= Line_MouseMove;
            paintSurface.MouseMove -= Shapes_MouseMove;
            foreach (UIElement el in paintSurface.Children)
            {
                el.MouseLeftButtonDown += Cursor_MouseDown;
                el.MouseLeftButtonUp += Cursor_MouseUp;
                el.MouseMove += Cursor_MouseMove;
            }
        }
        
        private HitType SetHitType(Shape shap, Point point)
        {//определяем где расположен курсор на объекте
            if (shap == null) return HitType.None;
            double left = Canvas.GetLeft(shap);
            double top = Canvas.GetTop(shap);
            double right = left + shap.Width;
            double bottom = top + shap.Height;
            if (point.X < left) return HitType.None;
            if (point.X > right) return HitType.None;
            if (point.Y < top) return HitType.None;
            if (point.Y > bottom) return HitType.None;

            const double GAP = 10;
            if (point.X - left < GAP)
            {
                if (point.Y - top < GAP) return HitType.UL;
                if (bottom - point.Y < GAP) return HitType.LL;
                return HitType.L;
            }
            else if (right - point.X < GAP)
            {
                if (point.Y - top < GAP) return HitType.UR;
                if (bottom - point.Y < GAP) return HitType.LR;
                return HitType.R;
            }
            if (point.Y - top < GAP) return HitType.T;
            if (bottom - point.Y < GAP) return HitType.B;
            return HitType.Body;
        }
        private void SetMouseCursor()
        {//вид курсора в зависимости от положения
            Cursor desiredCursor = Cursors.Arrow;
            switch (MouseHitType)
            {
                case HitType.None://вне обЪекта
                    desiredCursor = Cursors.Arrow;
                    break;
                case HitType.Body://внутри обЪекта
                    desiredCursor = Cursors.ScrollAll;
                    break;
                case HitType.UL://левый верхний край
                case HitType.LR://нижний правый
                    desiredCursor = Cursors.SizeNWSE;
                    break;
                case HitType.LL://нижний левый
                case HitType.UR://верхний правый
                    desiredCursor = Cursors.SizeNESW;
                    break;
                case HitType.T://вверх
                case HitType.B://низ
                    desiredCursor = Cursors.SizeNS;
                    break;
                case HitType.L://лево
                case HitType.R://право
                    desiredCursor = Cursors.SizeWE;
                    break;
            }
             Cursor = desiredCursor;
        }
        private void Cursor_MouseDown(object sender, MouseButtonEventArgs e)
        {//при нажатии на объект в режиме курсора
            UIElement obj = sender as UIElement;
            paintSurface.Children.Remove(obj);
            paintSurface.Children.Add(obj);
            Shape shap = selectedObj as Shape;
            if (shap != null)
                shap.Opacity = 1;
            shap = obj as Shape;
            selectedObj = obj;
            shap.Opacity = 0.7;

            MouseHitType = SetHitType(shap, Mouse.GetPosition(paintSurface));
            SetMouseCursor();
            if (MouseHitType == HitType.None) return;
            LastPoint = Mouse.GetPosition(paintSurface);
            isDragInProgress = true;
        }
        private void Cursor_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragInProgress = false;
        }
        private void Cursor_MouseMove(object sender, MouseEventArgs e)
        {//при движении мыши в режиме крусора
            var obj = selectedObj as Shape;
            if (isDragInProgress)
            {
                Point point = Mouse.GetPosition(paintSurface);
                double offset_x = point.X - LastPoint.X;
                double offset_y = point.Y - LastPoint.Y;

                double new_x = Canvas.GetLeft(obj);
                double new_y = Canvas.GetTop(obj);
                double new_width = obj.Width;
                double new_height = obj.Height;
                //изменение размеров и перемещение
                if ((new_x + new_width + offset_x) < paintSurface.ActualWidth
                    && (new_x + offset_x) > 0
                    && (new_y + new_height + offset_y) < paintSurface.ActualHeight
                    && (new_y + offset_y) > 0)
                    switch (MouseHitType)
                    {
                        case HitType.Body:
                            new_x += offset_x;
                            new_y += offset_y;
                            break;
                        case HitType.UL:
                            new_x += offset_x;
                            new_y += offset_y;
                            new_width -= offset_x;
                            new_height -= offset_y;
                            break;
                        case HitType.UR:
                            new_y += offset_y;
                            new_width += offset_x;
                            new_height -= offset_y;
                            break;
                        case HitType.LR:
                            new_width += offset_x;
                            new_height += offset_y;
                            break;
                        case HitType.LL:
                            new_x += offset_x;
                            new_width -= offset_x;
                            new_height += offset_y;
                            break;
                        case HitType.L:
                            new_x += offset_x;
                            new_width -= offset_x;
                            break;
                        case HitType.R:
                            new_width += offset_x;
                            break;
                        case HitType.B:
                            new_height += offset_y;
                            break;
                        case HitType.T:
                            new_y += offset_y;
                            new_height -= offset_y;
                            break;
                    }

                if ((new_width > 0) && (new_height > 0))
                {
                    Canvas.SetLeft(obj, new_x);
                    Canvas.SetTop(obj, new_y);
                    obj.Width = new_width;
                    obj.Height = new_height;

                    LastPoint = point;
                }
            }
            else
            {
                MouseHitType = SetHitType(obj, Mouse.GetPosition(paintSurface));
                SetMouseCursor();
            }
        }
        private void AddRightMouseClickEvent(UIElement element)
        {//добавление события нажатия на правую кнопку мыши
            element.MouseRightButtonDown += RightMouseClick;
        }
        private void RightMouseClick(object sender, MouseEventArgs e)
        {//нажатие правой кнопки мыши на объект
            var curObj = (UIElement)sender;
            ContextMenu cm = myWin.FindResource("contextMenu") as ContextMenu;
            cm.PlacementTarget = curObj;
            cm.IsOpen = true;
        }
        private void DeleteItem(object sender, RoutedEventArgs e)
        {//кнопка удаления
            MenuItem mi = (MenuItem)sender;
            ContextMenu cm = mi.Parent as ContextMenu;
            var item = (UIElement)cm.PlacementTarget;
            paintSurface.Children.Remove(item);
        }
        private void CopyItem(object sender, RoutedEventArgs e)
        {//кнопка копирования
            MenuItem mi = (MenuItem)sender;
            ContextMenu cm = mi.Parent as ContextMenu;
            copyObj = (UIElement)cm.PlacementTarget;
        }
        double oldAngle = 0;
        private void RotateItem(object sender, RoutedEventArgs e)
        {//вращение объекта
            MenuItem mi = (MenuItem)sender;
            MenuItem par = mi.Parent as MenuItem;
            ContextMenu cm = par.Parent as ContextMenu;
            var item = (Shape)cm.PlacementTarget;
            item.RenderTransformOrigin = new Point(0.5, 0.5);
            RotateTransform rot = new RotateTransform(oldAngle + Convert.ToDouble(mi.Tag));
            //if (mi.Tag.ToString() == "90" || mi.Tag.ToString() == "-90")
            //{
            //    if (item.Width > item.Height)
            //    {
            //        Canvas.SetTop(item, Canvas.GetTop(item) - Math.Abs(item.Width - item.Height) / 2);
            //        Canvas.SetLeft(item, Canvas.GetLeft(item) + Math.Abs(item.Width - item.Height) / 2);
            //    }
            //    else
            //    {
            //        Canvas.SetTop(item, Canvas.GetTop(item) + Math.Abs(item.Width - item.Height) / 2);
            //        Canvas.SetLeft(item, Canvas.GetLeft(item) - Math.Abs(item.Width - item.Height) / 2);
            //    }
            //    double temp = item.Width;
            //    item.Width = item.Height;
            //    item.Height = temp;

            //}
            oldAngle += Convert.ToDouble(mi.Tag);
            item.RenderTransform = rot;

        }
        private void lbColors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {//изменение цвета
            BrushConverter bc = new BrushConverter();
            Shape shap = selectedObj as Shape;
            Brush brush = (Brush)bc.ConvertFrom((string)lbColors.SelectedItem);
            if (lbColorPicker.SelectedIndex == 0)
            {
                strokeColor.Fill = brush;
                if (shap != null)
                    shap.Stroke = brush;
            }
            else
            {
                fillColor.Fill = brush;
                if (shap != null)
                    shap.Fill = brush;
            }
        }
        private void cbTypeStroke_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {//изменение типа штриха
            Shape el = selectedObj as Shape;
            Types tp = (Types)cbTypeStroke.SelectedItem;
            if (el != null)
                el.StrokeDashArray = tp.Dtype;
        }
        private void cbSizeStroke_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {//изменение толщины штриха
            Shape el = selectedObj as Shape;
            if (el != null)
                el.StrokeThickness = cbSizeStroke.SelectedIndex + 1;
        }
        private void lbShapes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {//выбор фигуры
            paintSurface.MouseMove -= Draw_MouseMove;
            paintSurface.MouseMove -= Shapes_MouseMove;
            paintSurface.MouseMove -= Line_MouseMove;
            if (lbShapes.SelectedIndex == 0)
            {
                paintSurface.MouseMove += Line_MouseMove;
            }
            else
            {
                paintSurface.MouseMove += Shapes_MouseMove;
            }
            Shape shap = selectedObj as Shape;
            if (shap != null)
                shap.Opacity = 1;
            selectedObj = null;
            foreach (UIElement el in paintSurface.Children)
                el.MouseLeftButtonDown -= Cursor_MouseDown;
        }
        private void paintSurface_MouseDown(object sender, MouseButtonEventArgs e)
        {//запомниание начальных координат
            if (e.ButtonState == MouseButtonState.Pressed)
                currentPoint = e.GetPosition(this);
        }
        private void Line_MouseMove(object sender, MouseEventArgs e)
        {//риосвание линии
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Line oldLine = currObj as Line;
                paintSurface.Children.Remove(oldLine);
                Line line = new Line();
                line.Stroke = strokeColor.Fill;
                line.StrokeThickness = cbSizeStroke.SelectedIndex+1;
                Types tp = (Types)cbTypeStroke.SelectedItem;
                line.StrokeDashArray =  tp.Dtype;
                line.X1 = currentPoint.X;
                line.Y1 = currentPoint.Y - stPanel.ActualHeight;
                line.X2 = e.GetPosition(this).X;
                line.Y2 = e.GetPosition(this).Y - stPanel.ActualHeight;
                AddRightMouseClickEvent(line);
                paintSurface.Children.Add(line);
                currObj = line;
            }
            else currObj = null;
        }
        private void Shapes_MouseMove(object sender, MouseEventArgs e)
        {//рисование шейпов
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Shape old_el = currObj as Shape;
                Shape el=new Ellipse();
                switch(lbShapes.SelectedIndex)
                {
                    case 1:
                        el = new Ellipse();
                        break;
                    case 2:
                        el = new Rectangle();
                        break;
                    case 3:
                        Rectangle temp = new Rectangle();
                        temp.RadiusX = 20;
                        temp.RadiusY = 20;
                        el = temp;
                        break;
                    default: break;
                }
                paintSurface.Children.Remove(old_el);
                el.Height = Math.Abs(e.GetPosition(this).Y - currentPoint.Y);
                el.Width = Math.Abs(e.GetPosition(this).X - currentPoint.X);
                el.Stroke = strokeColor.Fill;
                el.Fill = fillColor.Fill;
                el.StrokeThickness = cbSizeStroke.SelectedIndex + 1;
                Types tp = (Types)cbTypeStroke.SelectedItem;
                el.StrokeDashArray = tp.Dtype;
                AddRightMouseClickEvent(el);

                paintSurface.Children.Add(el);
                if ((e.GetPosition(this).X - currentPoint.X) > 0)
                    Canvas.SetLeft(el, currentPoint.X);
                else Canvas.SetLeft(el, e.GetPosition(this).X);
                if ((e.GetPosition(this).Y - currentPoint.Y) > 0)
                    Canvas.SetTop(el, currentPoint.Y - stPanel.ActualHeight);
                else Canvas.SetTop(el, e.GetPosition(this).Y - stPanel.ActualHeight);
                currObj = el;
            }
            else currObj = null;
        }
        private void DockPanel_MouseMove(object sender, MouseEventArgs e)
        {//определение координат мыши
            Point p = e.GetPosition(this);
            tbCursorPosition.Text = p.X + ",  " + (p.Y - stPanel.ActualHeight) + "пкс";
        }
        private void DockPanel_MouseLeave(object sender, MouseEventArgs e)
        {//при выходе из канваса координаты мыши не определяются
            tbCursorPosition.Text = "";
        }
        void Thumb_onDragStarted(object sender, DragStartedEventArgs e)
        {//при нажатии на кнопку изменения размера канваса
            btnCursor_OnClick(sender, e);
        }
        void Thumb_onDragDelta(object sender, DragDeltaEventArgs e)
        {//изменение размера канваса
            double yadjust = paintSurface.Height + e.VerticalChange;
            double xadjust = paintSurface.Width + e.HorizontalChange;
            if ((xadjust >= 0) && (yadjust >= 0))
            {
                paintSurface.Width = xadjust;
                paintSurface.Height = yadjust;
                if (paintSurface.Width >= 400)
                    myGrid.Width = paintSurface.Width + 10;
                else myGrid.Width = 400;
                if (paintSurface.Height >= 200)
                    myGrid.Height = paintSurface.Height + 10;
                else myGrid.Height = 200;
                Canvas.SetLeft(myThumb, Canvas.GetLeft(myThumb) + e.HorizontalChange);
                Canvas.SetTop(myThumb, Canvas.GetTop(myThumb) + e.VerticalChange);
                tbCanvasSize.Text =paintSurface.Width.ToString() +" x " +
                                paintSurface.Height.ToString()+"пкс";
            }
        }
    }
}
