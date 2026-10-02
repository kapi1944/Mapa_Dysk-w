using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MapaDyskow {
public sealed class DaneInformacji {
    public Dictionary<string,string> Multimedia=new Dictionary<string,string>();
    public BitmapSource Podglad; public string Blad;
}
public static class MetadaneInformacji {
    static readonly object blokada=new object();
    static readonly Dictionary<string,Task<DaneInformacji>> pamiec=new Dictionary<string,Task<DaneInformacji>>(StringComparer.OrdinalIgnoreCase);
    public static Task<DaneInformacji> Pobierz(Wiersz element){
        var plik=new FileInfo(element.Sciezka);string klucz=plik.FullName+"|"+plik.Length+"|"+plik.LastWriteTimeUtc.Ticks;
        lock(blokada){Task<DaneInformacji> zadanie;if(pamiec.TryGetValue(klucz,out zadanie))return zadanie;
            zadanie=Task.Run(()=>Odczytaj(element));pamiec[klucz]=zadanie;return zadanie;}
    }
    static DaneInformacji Odczytaj(Wiersz element){var dane=new DaneInformacji();try{
        var plik=new FileInfo(element.Sciezka);if((plik.Attributes&(FileAttributes.Offline|FileAttributes.ReparsePoint))!=0||(((int)plik.Attributes)&0x440000)!=0){dane.Blad="Metadane niedostępne dla pliku offline lub dowiązania.";return dane;}
        dane.Multimedia=MetadaneWindows.PobierzRozszerzone(element.Sciezka);
        if(element.TypTekst=="Zdjęcie"&&plik.Length<=128*1024*1024){using(var strumien=new FileStream(plik.FullName,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
            var dekoder=BitmapDecoder.Create(strumien,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);var ramka=dekoder.Frames[0];int szerokosc=ramka.PixelWidth,wysokosc=ramka.PixelHeight;
            dane.Multimedia["Szerokość"]=szerokosc+" px";dane.Multimedia["Wysokość"]=wysokosc+" px";dane.Multimedia["Megapiksele"]=((double)szerokosc*wysokosc/1000000).ToString("0.##")+" MP";dane.Multimedia["Proporcje obrazu"]=((double)szerokosc/wysokosc).ToString("0.###")+":1";dane.Multimedia["Głębia kolorów"]=ramka.Format.BitsPerPixel+" bit";dane.Multimedia["DPI"]=ramka.DpiX.ToString("0.##")+" × "+ramka.DpiY.ToString("0.##");
            if(ramka.ColorContexts!=null&&ramka.ColorContexts.Count>0)dane.Multimedia["Profil kolorów"]=ramka.ColorContexts[0].ProfileUri==null?"Osadzony profil ICC":ramka.ColorContexts[0].ProfileUri.ToString();
            if((long)szerokosc*wysokosc<=100000000){strumien.Position=0;var obraz=new BitmapImage();obraz.BeginInit();obraz.CacheOption=BitmapCacheOption.OnLoad;obraz.StreamSource=strumien;if(szerokosc>=wysokosc)obraz.DecodePixelWidth=1000;else obraz.DecodePixelHeight=1000;obraz.EndInit();obraz.Freeze();dane.Podglad=obraz;}
        }}
        if(element.TypTekst=="Film"){var meta=Kafelki.Metadane(element,CancellationToken.None).GetAwaiter().GetResult();dane.Podglad=meta.Miniatura;if(meta.Szerokosc>0&&meta.Wysokosc>0)dane.Multimedia["Proporcje obrazu"]=((double)meta.Szerokosc/meta.Wysokosc).ToString("0.###")+":1";}
    }catch(Exception blad){dane.Blad=blad.Message;}return dane;}
}
public sealed partial class OknoGlowne {
    CancellationTokenSource anulowanieInformacji;int wersjaInformacji;bool zmianaSzerokosci;
    public static double OgraniczSzerokoscPanelu(double szerokosc,double dostepna){double maksimum=Math.Max(180,Math.Min(560,dostepna-360));return Math.Max(180,Math.Min(maksimum,Double.IsNaN(szerokosc)?360:szerokosc));}
    void PrzygotujPanelInformacji(){
        Kontrolka<Button>("ZamknijInformacje").Click+=(s,e)=>{Kafelki.Ustawienia.PanelSzczegolow=false;PokazPanelSzczegolow();Kafelki.Zapisz();};
        var separator=Kontrolka<GridSplitter>("SeparatorInformacji");separator.DragCompleted+=(s,e)=>{if(!Kafelki.Ustawienia.PanelSzczegolow)return;var panel=Kontrolka<Border>("PanelSzczegolow");Kafelki.Ustawienia.SzerokoscPanelu=panel.ActualWidth;PokazPanelSzczegolow();Kafelki.Zapisz();};
        Okno.SizeChanged+=(s,e)=>DopasujPanel();
    }
    void DopasujPanel(){if(zmianaSzerokosci)return;zmianaSzerokosci=true;try{var panel=Kontrolka<Border>("PanelSzczegolow");var siatka=(Grid)panel.Parent;bool widoczny=Kafelki.Ustawienia.PanelSzczegolow;double dostepna=Math.Max(540,(siatka.ActualWidth>0?siatka.ActualWidth:Okno.Width)-siatka.ColumnDefinitions[0].ActualWidth-10);var kolumna=siatka.ColumnDefinitions[4];kolumna.MinWidth=widoczny?180:0;kolumna.MaxWidth=widoczny?OgraniczSzerokoscPanelu(560,dostepna):0;kolumna.Width=new GridLength(widoczny?OgraniczSzerokoscPanelu(Kafelki.Ustawienia.SzerokoscPanelu,dostepna):0);siatka.ColumnDefinitions[3].Width=new GridLength(widoczny?5:0);panel.Visibility=widoczny?Visibility.Visible:Visibility.Collapsed;Kontrolka<GridSplitter>("SeparatorInformacji").Visibility=panel.Visibility;}finally{zmianaSzerokosci=false;}}
    void PokazPanelSzczegolow(){DopasujPanel();PokazInformacjeZaznaczenia();}
    void PokazInformacjeZaznaczenia(){
        var wybor=lista.Visibility==Visibility.Visible?lista.SelectedItems.Cast<Wiersz>().ToList():zaznaczone.ToList();
        if(wybor.Count==1){PokazSzczegoly(wybor[0]);return;}
        RozpocznijInformacje();if(!Kafelki.Ustawienia.PanelSzczegolow)return;
        if(wybor.Count==0){szczegoly.Children.Add(Program.Tekst("Wybierz element, aby zobaczyć informacje.",14,"#A4B2C8"));return;}
        var sekcja=SekcjaInformacji("PODSTAWOWE");WartoscInformacji(sekcja,"Zaznaczono",wybor.Count+" elementów");WartoscInformacji(sekcja,"Pliki",wybor.Count(w=>w.Rodzaj=="plik").ToString());WartoscInformacji(sekcja,"Foldery",wybor.Count(w=>w.Rodzaj=="folder").ToString());
        // Nie sumujemy rodzica i jego potomków drugi raz.
        var niezalezne=wybor.Where(w=>!wybor.Any(p=>p!=w&&p.Rodzaj=="folder"&&w.Sciezka.StartsWith(p.Sciezka.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))).ToList();
        WartoscInformacji(sekcja,"Łączny znany rozmiar",Program.Rozmiar(niezalezne.Where(w=>w.Rozmiar>=0).Sum(w=>w.Rozmiar)));if(wybor.Any(w=>w.Rozmiar<0))WartoscInformacji(sekcja,"Bez znanego rozmiaru",wybor.Count(w=>w.Rozmiar<0).ToString());
        foreach(string typ in new[]{"Zdjęcie","Film","Dokument","Arkusz","Audio"}){int liczba=wybor.Count(w=>w.TypTekst==typ);if(liczba>0)WartoscInformacji(sekcja,typ,liczba.ToString());}WartoscInformacji(sekcja,"Duplikaty",wybor.Count(w=>w.Kopie>1).ToString());
    }
    void RozpocznijInformacje(){++wersjaInformacji;if(anulowanieInformacji!=null){anulowanieInformacji.Cancel();anulowanieInformacji.Dispose();}anulowanieInformacji=new CancellationTokenSource();szczegoly.Children.Clear();}
    StackPanel SekcjaInformacji(string nazwa){bool rozwinieta;var zawartosc=new StackPanel();var sekcja=new Expander {Header=nazwa,Content=zawartosc,IsExpanded=!Kafelki.Ustawienia.SekcjePanelu.TryGetValue(nazwa,out rozwinieta)||rozwinieta,Margin=new Thickness(0,8,0,4),Foreground=Brushes.White};sekcja.Expanded+=(s,e)=>{Kafelki.Ustawienia.SekcjePanelu[nazwa]=true;Kafelki.Zapisz();};sekcja.Collapsed+=(s,e)=>{Kafelki.Ustawienia.SekcjePanelu[nazwa]=false;Kafelki.Zapisz();};szczegoly.Children.Add(sekcja);return zawartosc;}
    static void WartoscInformacji(StackPanel panel,string nazwa,string wartosc){if(String.IsNullOrWhiteSpace(wartosc))return;panel.Children.Add(Program.Tekst(nazwa,10,"#91A8CD"));panel.Children.Add(Program.Tekst(wartosc,13,"#E7ECF4"));}
    async void PokazSzczegoly(Wiersz element){
        RozpocznijInformacje();int numerInformacji=wersjaInformacji;var token=anulowanieInformacji.Token;if(!Kafelki.Ustawienia.PanelSzczegolow)return;
        if(element.Rodzaj=="duplikat"){PokazGrupeDuplikatow(element);return;}
        szczegoly.Children.Add(Program.Tekst(element.Nazwa,21,"#EEF4FF"));var podstawowe=SekcjaInformacji("PODSTAWOWE");WartoscInformacji(podstawowe,"Pełna lokalizacja",element.Sciezka);WartoscInformacji(podstawowe,"Typ",element.TypTekst);WartoscInformacji(podstawowe,"Format",element.Rozszerzenie);WartoscInformacji(podstawowe,element.Rodzaj=="folder"?"Rozmiar rekursywny (indeks)":"Rozmiar",element.RozmiarTekst);
        var akcje=new WrapPanel();podstawowe.Children.Add(akcje);akcje.Children.Add(Program.Przycisk("Otwórz",()=>OtworzElement(element)));akcje.Children.Add(Program.Przycisk(element.Rodzaj=="folder"?"Otwórz w Eksploratorze Windows":"Otwórz lokalizację",()=>{if(element.Rodzaj=="folder")Program.Eksplorator(element.Sciezka);else OtworzFolder(Path.GetDirectoryName(element.Sciezka));}));akcje.Children.Add(Program.Przycisk("Kopiuj ścieżkę",()=>Program.Kopiuj(element.Sciezka)));akcje.Children.Add(Program.Przycisk("Zmień nazwę",()=>EdytujNazwe(element)));
        var daty=SekcjaInformacji("DATY");WartoscInformacji(daty,"Data utworzenia",element.UtworzenieTekst);WartoscInformacji(daty,"Data modyfikacji / aktywności",element.DataTekst);if(element.Dostep.HasValue)WartoscInformacji(daty,"Ostatni dostęp (Windows)",element.Dostep.Value.ToString("yyyy-MM-dd HH:mm"));
        if(element.Atrybuty.HasValue){var atrybuty=SekcjaInformacji("ATRYBUTY");var nazwy=new List<string>();foreach(var para in new[]{Tuple.Create(FileAttributes.ReadOnly,"Tylko do odczytu"),Tuple.Create(FileAttributes.Hidden,"Ukryty"),Tuple.Create(FileAttributes.System,"Systemowy"),Tuple.Create(FileAttributes.Archive,"Archiwalny"),Tuple.Create(FileAttributes.Compressed,"Skompresowany"),Tuple.Create(FileAttributes.Encrypted,"Zaszyfrowany"),Tuple.Create(FileAttributes.ReparsePoint,"Dowiązanie"),Tuple.Create(FileAttributes.Offline,"Offline")})if((element.Atrybuty.Value&para.Item1)!=0)nazwy.Add(para.Item2);WartoscInformacji(atrybuty,"Atrybuty",nazwy.Count==0?"Zwykły":String.Join(" · ",nazwy));}
        if(!String.IsNullOrEmpty(element.Oznaczenia)||!String.IsNullOrEmpty(element.Uwagi)){var analiza=SekcjaInformacji("ANALIZA");var znaczniki=new WrapPanel();foreach(string tag in (element.Oznaczenia??"").Split(new[]{" · "},StringSplitOptions.RemoveEmptyEntries))znaczniki.Children.Add(new Border {Background=Program.Kolor("#30435B"),CornerRadius=new CornerRadius(4),Margin=new Thickness(0,2,5,4),Padding=new Thickness(5),Child=Program.Tekst(tag,10,"#D5E4FA")});analiza.Children.Add(znaczniki);WartoscInformacji(analiza,"Uwagi",element.Uwagi);if(element.WIndeksie)WartoscInformacji(analiza,"Źródło statystyk","Indeks audytu — rozmiary i analiza mogą być nieaktualne.");}
        var oczekiwanie=Program.Tekst("Ładowanie danych…",12,"#91A8CD");szczegoly.Children.Add(oczekiwanie);
        try{
            if(element.Rodzaj=="folder"){
                var dane=await Task.Run(()=>DaneFolderu(element));if(token.IsCancellationRequested||numerInformacji!=wersjaInformacji)return;
                foreach(var para in dane.Item1)WartoscInformacji(podstawowe,para.Key,para.Value);
                if(element.WIndeksie&&element.Rozmiar>0){var zawartosc=SekcjaInformacji("ZAWARTOŚĆ");string[] nazwy={"Filmy","Zdjęcia","Dokumenty","Archiwa","Inne"};for(int i=0;i<5;i++)if(element.Typy[i]>0)WartoscInformacji(zawartosc,nazwy[i],Program.Rozmiar(element.Typy[i])+" · "+(100.0*element.Typy[i]/element.Rozmiar).ToString("0.0")+"%");}
                if(dane.Item2.Count>0){var najwieksze=SekcjaInformacji("NAJWIĘKSZE ELEMENTY");foreach(var w in dane.Item2)najwieksze.Children.Add(Program.Przycisk(w.Nazwa+" · "+w.RozmiarTekst,()=>OtworzElement(w)));}
                if(dane.Item3.Count>0){var duplikaty=SekcjaInformacji("DUPLIKATY");foreach(var para in dane.Item3)WartoscInformacji(duplikaty,para.Key,para.Value);}
                if(Path.GetPathRoot(element.Sciezka).TrimEnd('\\').Equals(element.Sciezka.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)){var dysk=await Task.Run(()=>new DriveInfo(element.Sciezka));if(token.IsCancellationRequested)return;var sekcja=SekcjaInformacji("DYSK");WartoscInformacji(sekcja,"Pojemność",Program.Rozmiar(dysk.TotalSize));WartoscInformacji(sekcja,"Wolne",Program.Rozmiar(dysk.AvailableFreeSpace));WartoscInformacji(sekcja,"Zajęte",Program.Rozmiar(dysk.TotalSize-dysk.TotalFreeSpace));WartoscInformacji(sekcja,"System plików",dysk.DriveFormat);WartoscInformacji(sekcja,"Etykieta",dysk.VolumeLabel);sekcja.Children.Add(new ProgressBar {Minimum=0,Maximum=100,Value=100.0*(dysk.TotalSize-dysk.TotalFreeSpace)/dysk.TotalSize,Height=8});}
            }else{
                var dane=await MetadaneInformacji.Pobierz(element);if(token.IsCancellationRequested||numerInformacji!=wersjaInformacji)return;
                if(dane.Podglad!=null)szczegoly.Children.Insert(1,new Image {Source=dane.Podglad,MaxHeight=260,Stretch=Stretch.Uniform,Margin=new Thickness(0,10,0,8)});
                if(dane.Multimedia.Count>0){var multimedia=SekcjaInformacji("MULTIMEDIA / DOKUMENT");foreach(var para in dane.Multimedia)WartoscInformacji(multimedia,para.Key,para.Value);}if(dane.Blad!=null)szczegoly.Children.Add(Program.Tekst(dane.Blad,12,"#F0BB6D"));
                if(element.Kopie>1){var kopie=await Task.Run(()=>{using(var baza=new Baza(Program.Indeks,false))return Tuple.Create(baza.Zapytaj("SELECT sciezka FROM lokalizacje_duplikatow WHERE grupa=? ORDER BY sciezka LIMIT 100",element.Grupa),baza.Wartosc("SELECT potencjal FROM grupy_duplikatow WHERE id=?",element.Grupa));});if(token.IsCancellationRequested)return;var duplikaty=SekcjaInformacji("DUPLIKATY");WartoscInformacji(duplikaty,"Potwierdzone kopie",element.Kopie.ToString());WartoscInformacji(duplikaty,"Potencjalnie odzyskiwane (grupa)",Program.Rozmiar(Convert.ToInt64(kopie.Item2)));foreach(var r in kopie.Item1)WartoscInformacji(duplikaty,"Lokalizacja",Convert.ToString(r[0]));duplikaty.Children.Add(Program.Przycisk("Porównaj kopie",()=>PokazKopie(element)));}
            }
        }catch(Exception blad){if(!token.IsCancellationRequested&&numerInformacji==wersjaInformacji)szczegoly.Children.Add(Program.Tekst("Część informacji niedostępna: "+blad.Message,12,"#F0BB6D"));}finally{if(numerInformacji==wersjaInformacji)szczegoly.Children.Remove(oczekiwanie);}
    }
    static Tuple<Dictionary<string,string>,List<Wiersz>,Dictionary<string,string>> DaneFolderu(Wiersz element){
        var podstawowe=new Dictionary<string,string>();var duplikaty=new Dictionary<string,string>();List<Wiersz> najwieksze;
        using(var baza=new Baza(Program.Indeks,false)){
            // Bez schodzenia do podfolderów i bez hashowania.
            var bezposrednie=EksploratorFolderow.Odczytaj(baza,element.Sciezka);podstawowe["Rozmiar bezpośredni"]=Program.Rozmiar(bezposrednie.Where(w=>w.Rodzaj=="plik").Sum(w=>Math.Max(0,w.Rozmiar)));podstawowe["Bezpośrednie pliki"]=bezposrednie.Count(w=>w.Rodzaj=="plik").ToString();podstawowe["Bezpośrednie podfoldery"]=bezposrednie.Count(w=>w.Rodzaj=="folder").ToString();if(element.WIndeksie){podstawowe["Pliki łącznie (indeks)"]=element.PlikiTekst;podstawowe["Foldery łącznie (indeks)"]=element.PodfolderyTekst;}
            najwieksze=bezposrednie.Where(w=>w.WIndeksie&&w.Rozmiar>=0).OrderByDescending(w=>w.Rozmiar).Take(5).ToList();
            if(element.Duplikaty>0){var r=baza.Zapytaj("SELECT count(*),count(DISTINCT grupa) FROM lokalizacje_duplikatow WHERE folder=? OR sciezka LIKE ? ESCAPE '^'",element.Sciezka,Program.Prefiks(element.Sciezka))[0];duplikaty["Potwierdzone lokalizacje wewnątrz (indeks)"]=Convert.ToString(r[0]);duplikaty["Grupy duplikatów"]=Convert.ToString(r[1]);duplikaty["Rozmiar zawartości w grupach"]=Program.Rozmiar(element.Duplikaty);}
        }return Tuple.Create(podstawowe,najwieksze,duplikaty);
    }
}
}
