using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MapaDyskow {
public sealed partial class OknoGlowne {
    public void TestujIkony(string zrzuty){
        string katalog=Path.Combine(zrzuty,"fixture-ikon");Directory.CreateDirectory(katalog);Directory.CreateDirectory(Path.Combine(katalog,"Folder"));
        foreach(string nazwa in new[]{"Tekst.txt","Dokument.pdf","Zdjecie.jpg","Obraz.png","Film.mp4","Audio.mp3","Archiwum.zip","Bez rozszerzenia","Nieznany.xyz987"})File.WriteAllText(Path.Combine(katalog,nazwa),"fixture ikon typu");
        File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"),Path.Combine(katalog,"Program.exe"));
        var dane=Directory.GetFileSystemEntries(katalog).Select(s=>new Wiersz {Sciezka=s,Nazwa=Path.GetFileName(s),Rodzaj=Directory.Exists(s)?"folder":"plik"}).ToList();
        foreach(var element in dane){var obraz=IkonyPlikow.Pobierz(element,20,CancellationToken.None).GetAwaiter().GetResult();TestyOperacji.Wymagaj(obraz!=null&&obraz.IsFrozen,"Shell: "+element.Nazwa);TestyOperacji.Wymagaj(Object.ReferenceEquals(obraz,IkonyPlikow.Pobierz(element,20,CancellationToken.None).GetAwaiter().GetResult()),"Cache: "+element.Nazwa);}
        var exe=dane.First(w=>w.Nazwa=="Program.exe");TestyOperacji.Wymagaj(IkonyPlikow.Pobierz(exe,40,CancellationToken.None).Result.PixelWidth>=32,"Większa ikona EXE z konkretnego pliku");
        var tekst=dane.First(w=>w.Nazwa=="Tekst.txt");var wspolna=IkonyPlikow.Pobierz(tekst,20,CancellationToken.None).Result;
        TestyOperacji.Wymagaj(Object.ReferenceEquals(wspolna,IkonyPlikow.Pobierz(new Wiersz {Sciezka="inna-sciezka.txt",Rodzaj="plik"},20,CancellationToken.None).Result),"Wspólny cache rozszerzenia");
        for(int i=0;i<520;i++)IkonyPlikow.Pobierz(new Wiersz {Sciezka=Path.Combine(katalog,"brak-"+i+".exe"),Rodzaj="plik"},20,CancellationToken.None).GetAwaiter().GetResult();
        var pamiec=(System.Collections.IDictionary)typeof(IkonyPlikow).GetField("pamiec",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);TestyOperacji.Wymagaj(pamiec.Count<=512,"Limit cache także dla błędów Shell");
        using(var anuluj=new CancellationTokenSource()){anuluj.Cancel();bool anulowano=false;try{IkonyPlikow.Pobierz(exe,20,anuluj.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){anulowano=true;}TestyOperacji.Wymagaj(anulowano,"Anulowanie żądania ikony");}
        var brak=new Wiersz {Sciezka=Path.Combine(katalog,"brak.exe"),Rodzaj="plik"};TestyOperacji.Wymagaj(IkonyPlikow.Pobierz(brak,20,CancellationToken.None).Result==null&&IkonyPlikow.Zastepcza(brak)!=null,"Fallback po błędzie Shell");
        foreach(string nazwa in new[]{"x.jpg","x.mp4","x.mp3","x.pdf","x.xlsx","x.zip","x.exe","x.iso","x.cs","x.xyz"})TestyOperacji.Wymagaj(IkonyPlikow.Zastepcza(new Wiersz {Sciezka=nazwa,Rodzaj="plik"})!=null,"Fallback kategorii "+nazwa);
        ostatnie=dane;lista.ItemsSource=dane;lista.SelectedItem=null;Okno.UpdateLayout();
        var kolumna=(DataGridTemplateColumn)lista.Columns.First(k=>k.SortMemberPath=="Nazwa");kolumna.DisplayIndex=0;kolumna.Width=260;var po=kolumna.CellTemplate;
        kolumna.CellTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Orientation='Horizontal'><TextBlock Text='{Binding SymbolSzczegolow}' FontFamily='Segoe MDL2 Assets' Foreground='#D6A84B' Width='25' VerticalAlignment='Center'/><TextBlock Text='{Binding Nazwa}' VerticalAlignment='Center'/></StackPanel></DataTemplate>");
        Renderuj(Okno,Path.Combine(zrzuty,"40-ikony-przed.png"));kolumna.CellTemplate=po;Okno.UpdateLayout();
        Oczekuj(()=>Potomkowie(lista).OfType<IkonaPliku>().Count()>=dane.Count&&Potomkowie(lista).OfType<IkonaPliku>().All(i=>i.Source is BitmapSource));
        Renderuj(Okno,Path.Combine(zrzuty,"41-ikony-po.png"));
        var render=new RenderTargetBitmap(1800,1000,144,144,PixelFormats.Pbgra32);render.Render((Visual)Okno.Content);var koder=new PngBitmapEncoder();koder.Frames.Add(BitmapFrame.Create(render));using(var plik=File.Create(Path.Combine(zrzuty,"42-ikony-144dpi.png")))koder.Save(plik);
        File.WriteAllText(Path.Combine(zrzuty,"test-ikon.txt"),"PASS: 11 elementów fixture; Shell, cache, EXE, fallback; Szczegóły przed/po; render 144 DPI. Zmiany widoku i wirtualizacja 100000 modeli sprawdzane przez TestujWidoki. Render DPI nie potwierdza przenoszenia okna między monitorami.");
    }
}
}
