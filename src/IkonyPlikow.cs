using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MapaDyskow {
public static class IkonyPlikow {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Informacja {public IntPtr Ikona;public int Indeks;public uint Atrybuty;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Nazwa;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string Typ;}
    [DllImport("shell32.dll",EntryPoint="SHGetFileInfoW",CharSet=CharSet.Unicode)]static extern IntPtr PobierzInformacje(string sciezka,uint atrybuty,out Informacja informacje,uint rozmiar,uint flagi);
    [DllImport("user32.dll",EntryPoint="DestroyIcon")]static extern bool ZwolnijIkone(IntPtr ikona);
    [DllImport("ole32.dll",EntryPoint="CoInitializeEx")]static extern int UruchomCom(IntPtr zarezerwowane,uint tryb);
    [DllImport("ole32.dll",EntryPoint="CoUninitialize")]static extern void ZakonczCom();
    static readonly SemaphoreSlim pracownik=new SemaphoreSlim(1);
    static readonly Dictionary<string,BitmapSource> pamiec=new Dictionary<string,BitmapSource>(StringComparer.OrdinalIgnoreCase);
    static readonly Queue<string> kolejnosc=new Queue<string>();
    public static async Task<BitmapSource> Pobierz(Wiersz element,int rozmiar,CancellationToken odwolanie){
        string rozszerzenie=Path.GetExtension(element.Sciezka).ToLowerInvariant();
        bool wlasna=element.Rodzaj=="folder"||rozszerzenie==".exe"||rozszerzenie==".lnk"||rozszerzenie==".ico";
        string klucz=(wlasna?element.Sciezka+"|"+element.Rozmiar+"|"+element.Zmiana:rozszerzenie)+"|"+rozmiar;
        await pracownik.WaitAsync(odwolanie).ConfigureAwait(false);
        try{
            BitmapSource obraz;if(pamiec.TryGetValue(klucz,out obraz))return obraz;
            odwolanie.ThrowIfCancellationRequested();
            obraz=await Task.Run(()=>{
                int com=UruchomCom(IntPtr.Zero,0);Informacja dane=new Informacja();
                try{
                    if(rozmiar>32&&wlasna){var duza=MetadaneWindows.PobierzIkone(element.Sciezka,rozmiar);if(duza!=null)return duza;}
                    uint flagi=0x100u|(rozmiar<=20?1u:0u)|(wlasna?0u:0x10u);
                    if(PobierzInformacje(wlasna?element.Sciezka:"plik"+rozszerzenie,element.Rodzaj=="folder"?0x10u:0x80u,out dane,(uint)Marshal.SizeOf(typeof(Informacja)),flagi)==IntPtr.Zero||dane.Ikona==IntPtr.Zero)return null;
                    var wynik=Imaging.CreateBitmapSourceFromHIcon(dane.Ikona,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());wynik.Freeze();return wynik;
                }finally{if(dane.Ikona!=IntPtr.Zero)ZwolnijIkone(dane.Ikona);if(com>=0)ZakonczCom();}
            }).ConfigureAwait(false);
            if(pamiec.Count>=512)pamiec.Remove(kolejnosc.Dequeue());pamiec[klucz]=obraz;kolejnosc.Enqueue(klucz);return obraz;
        }finally{pracownik.Release();}
    }
    public static ImageSource Zastepcza(Wiersz element){
        string typ=element.Rodzaj=="folder"?"Folder":Kafelki.Kategoria(element);
        string symbol;string kolor;
        switch(typ){
            case "Folder":symbol="\uE8B7";kolor="#F4C35B";break;
            case "Zdjęcie":symbol="\uE91B";kolor="#76D5AC";break;
            case "Film":symbol="\uE714";kolor="#79BAFF";break;
            case "Audio":symbol="\uE8D6";kolor="#F69ABF";break;
            case "Arkusz":symbol="\uE80A";kolor="#79D791";break;
            case "Dokument":symbol="\uE8A5";kolor="#A4C9FF";break;
            case "Archiwum":symbol="\uE8F1";kolor="#C9A3F5";break;
            case "Program":symbol="\uE756";kolor="#62D4EA";break;
            case "Obraz dysku / backup":symbol="\uEDA2";kolor="#D7AE86";break;
            case "Kod / konfiguracja":symbol="\uE943";kolor="#F3AE73";break;
            default:symbol="\uE9CE";kolor="#D2D8E0";break;
        }
        var rysunek=new DrawingGroup();using(var kontekst=rysunek.Open()){
            var tekst=new FormattedText(symbol,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe MDL2 Assets"),20,Program.Kolor(kolor),1);kontekst.DrawText(tekst,new Point(0,0));
        }rysunek.Freeze();var obraz=new DrawingImage(rysunek);obraz.Freeze();return obraz;
    }
}
public sealed class IkonaPliku : Image {
    public static readonly DependencyProperty ElementProperty=DependencyProperty.Register("Element",typeof(Wiersz),typeof(IkonaPliku),new PropertyMetadata(null,(kontrolka,zmiana)=>((IkonaPliku)kontrolka).Odswiez()));
    public Wiersz Element {get{return (Wiersz)GetValue(ElementProperty);}set{SetValue(ElementProperty,value);}}
    CancellationTokenSource odwolanie;
    public IkonaPliku(){Stretch=Stretch.Uniform;Loaded+=(s,e)=>Odswiez();Unloaded+=(s,e)=>{if(odwolanie!=null)odwolanie.Cancel();};}
    protected override void OnDpiChanged(DpiScale poprzednia,DpiScale biezaca){base.OnDpiChanged(poprzednia,biezaca);Odswiez();}
    async void Odswiez(){
        if(odwolanie!=null)odwolanie.Cancel();var element=Element;if(element==null){Source=null;return;}Source=IkonyPlikow.Zastepcza(element);if(!IsLoaded)return;
        var biezace=new CancellationTokenSource();odwolanie=biezace;
        try{var transformacja=PresentationSource.FromVisual(this);double skala=transformacja==null?1:transformacja.CompositionTarget.TransformToDevice.M11;
            var obraz=await IkonyPlikow.Pobierz(element,(int)Math.Ceiling((Double.IsNaN(Width)?20:Width)*skala),biezace.Token);
            if(!biezace.IsCancellationRequested&&IsLoaded&&Object.ReferenceEquals(element,Element)&&obraz!=null)Source=obraz;
        }catch(OperationCanceledException){}catch(IOException){}catch(UnauthorizedAccessException){}catch(COMException){}
        finally{biezace.Dispose();if(Object.ReferenceEquals(odwolanie,biezace))odwolanie=null;}
    }
}
}
