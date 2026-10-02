using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MapaDyskow {
public static class TestyEksploratoraPanelu {
    public static void Przygotuj(string katalog,string obraz){
        Directory.CreateDirectory(katalog);File.Copy(obraz,Path.Combine(katalog,"zdjecie.png"));
        File.WriteAllText(Path.Combine(katalog,"nowy.txt"),"Plik spoza indeksu.");
        Directory.CreateDirectory(Path.Combine(katalog,"podfolder"));
        File.WriteAllText(Path.Combine(katalog,"podfolder","bez-skanowania.txt"),"Nie czytamy poddrzewa.");
        using(var zapis=new BinaryWriter(File.Create(Path.Combine(katalog,"audio.wav")))){
            zapis.Write(Encoding.ASCII.GetBytes("RIFF"));zapis.Write(16036);zapis.Write(Encoding.ASCII.GetBytes("WAVEfmt "));zapis.Write(16);zapis.Write((short)1);zapis.Write((short)1);zapis.Write(8000);zapis.Write(16000);zapis.Write((short)2);zapis.Write((short)16);zapis.Write(Encoding.ASCII.GetBytes("data"));zapis.Write(16000);zapis.Write(new byte[16000]);
        }
        ZapiszFilm(Path.Combine(katalog,"film.avi"));
        var dekoder=new PngBitmapDecoder(new Uri(obraz),BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
        var exif=new BitmapMetadata("jpg");exif.SetQuery("/app1/ifd/{ushort=271}","Aparat testowy");
        var koder=new JpegBitmapEncoder();koder.Frames.Add(BitmapFrame.Create(dekoder.Frames[0],null,exif,null));using(var zapis=File.Create(Path.Combine(katalog,"exif.jpg")))koder.Save(zapis);
    }
    static void Blok(BinaryWriter zapis,string nazwa,Action<BinaryWriter> zawartosc){zapis.Write(Encoding.ASCII.GetBytes(nazwa));long poczatek=zapis.BaseStream.Position;zapis.Write(0);zawartosc(zapis);long koniec=zapis.BaseStream.Position;zapis.BaseStream.Position=poczatek;zapis.Write((int)(koniec-poczatek-4));zapis.BaseStream.Position=koniec;if((koniec-poczatek-4)%2!=0)zapis.Write((byte)0);}
    // Mały, poprawny AVI RGB24: cztery sztuczne klatki, bez zewnętrznych kodeków.
    static void ZapiszFilm(string sciezka){using(var zapis=new BinaryWriter(File.Create(sciezka)))Blok(zapis,"RIFF",r=>{
        r.Write(Encoding.ASCII.GetBytes("AVI "));Blok(r,"LIST",h=>{h.Write(Encoding.ASCII.GetBytes("hdrl"));Blok(h,"avih",a=>{foreach(int liczba in new[]{500000,3840,0,16,4,0,1,1920,32,20,0,0,0,0})a.Write(liczba);});
            Blok(h,"LIST",s=>{s.Write(Encoding.ASCII.GetBytes("strl"));Blok(s,"strh",a=>{a.Write(Encoding.ASCII.GetBytes("vidsDIB "));a.Write(0);a.Write((short)0);a.Write((short)0);foreach(int liczba in new[]{0,1,2,0,4,1920,-1,0})a.Write(liczba);foreach(short liczba in new short[]{0,0,32,20})a.Write(liczba);});Blok(s,"strf",a=>{a.Write(40);a.Write(32);a.Write(20);a.Write((short)1);a.Write((short)24);foreach(int liczba in new[]{0,1920,0,0,0,0})a.Write(liczba);});});});
        Blok(r,"LIST",m=>{m.Write(Encoding.ASCII.GetBytes("movi"));for(int i=0;i<4;i++)Blok(m,"00db",a=>a.Write(Enumerable.Repeat((byte)(40+i*40),1920).ToArray()));});
        Blok(r,"idx1",a=>{for(int i=0;i<4;i++){a.Write(Encoding.ASCII.GetBytes("00db"));a.Write(16);a.Write(4+i*1928);a.Write(1920);}});
    });}
}
public sealed partial class OknoGlowne {
    static System.Collections.Generic.IEnumerable<TextBlock> TekstyInformacji(DependencyObject element){var tekst=element as TextBlock;if(tekst!=null)yield return tekst;foreach(var dziecko in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())foreach(var tresc in TekstyInformacji(dziecko))yield return tresc;}
    string TekstPanelu(){return String.Join("\n",TekstyInformacji(szczegoly).Select(t=>t.Text));}
    void PoczekajNaPanel(){Oczekuj(()=>!TekstPanelu().Contains("Ładowanie"));}
    public void TestujEksploratorPanel(string katalog,string zrzuty){
        Directory.CreateDirectory(zrzuty);long skany=Skaner.LiczbaSkanow,hashe=Skaner.LiczbaHashowan;string indeksPrzed;
        using(var baza=new Baza(Program.Indeks,false))indeksPrzed=Program.Json.Serialize(baza.Zapytaj("SELECT sciezka,rozmiar,sha256 FROM pliki ORDER BY sciezka"));
        OtworzFolder(katalog);Oczekuj(()=>wczytanaWersja==wersja&&ostatnie.Any(w=>w.Nazwa=="nowy.txt"));
        TestyOperacji.Wymagaj(ostatnie.Any(w=>w.Nazwa=="podfolder")&&ostatnie.All(w=>!w.WIndeksie),"Eksplorator pokazuje pliki i foldery bez indeksu");
        File.WriteAllText(Path.Combine(katalog,"po-F5.txt"),"Nowy plik.");Directory.CreateDirectory(Path.Combine(katalog,"po-F5-folder"));
        File.WriteAllText(Path.Combine(katalog,"podfolder","kolejny.txt"),"Bez rekursji.");
        WykonajSkrot("odswiez");Oczekuj(()=>wczytanaWersja==wersja&&ostatnie.Any(w=>w.Nazwa=="po-F5.txt")&&ostatnie.Any(w=>w.Nazwa=="po-F5-folder"));
        TestyOperacji.Wymagaj(!ostatnie.Any(w=>w.Nazwa=="kolejny.txt")&&Skaner.LiczbaSkanow==skany&&Skaner.LiczbaHashowan==hashe,"F5 nie skanuje poddrzewa ani nie hashuje");
        using(var baza=new Baza(Program.Indeks,false))TestyOperacji.Wymagaj(indeksPrzed==Program.Json.Serialize(baza.Zapytaj("SELECT sciezka,rozmiar,sha256 FROM pliki ORDER BY sciezka")),"F5 nie zmienia historycznego indeksu");
        Kafelki.Ustawienia.PanelSzczegolow=true;PokazPanelSzczegolow();wyborWidoku.SelectedItem="Lista szczegółowa";
        var zdjecie=ostatnie.First(w=>w.Nazwa=="zdjecie.png");lista.SelectedItem=zdjecie;PoczekajNaPanel();
        TestyOperacji.Wymagaj(TekstPanelu().Contains("Megapiksele")&&TekstPanelu().Contains("32 px")&&TekstPanelu().Contains("DPI")&&Potomkowie(szczegoly).OfType<Image>().Any(),"Zdjęcie: podgląd, rozdzielczość, proporcje, DPI");
        Renderuj(Okno,Path.Combine(zrzuty,"30-panel-zdjecie-szczegoly.png"));
        var pierwsze=MetadaneInformacji.Pobierz(zdjecie,CancellationToken.None);Oczekuj(()=>pierwsze.IsCompleted);var drugie=MetadaneInformacji.Pobierz(zdjecie,CancellationToken.None);Oczekuj(()=>drugie.IsCompleted);
        TestyOperacji.Wymagaj(Object.ReferenceEquals(pierwsze.GetAwaiter().GetResult(),drugie.GetAwaiter().GetResult()),"Cache panelu dla ścieżki, rozmiaru i daty");
        File.SetLastWriteTimeUtc(zdjecie.Sciezka,File.GetLastWriteTimeUtc(zdjecie.Sciezka).AddSeconds(2));var zmienione=MetadaneInformacji.Pobierz(zdjecie,CancellationToken.None);Oczekuj(()=>zmienione.IsCompleted);
        TestyOperacji.Wymagaj(!Object.ReferenceEquals(pierwsze.Result,zmienione.GetAwaiter().GetResult()),"Zmiana LastWriteTime unieważnia cache panelu");
        DateTime dataPliku=File.GetLastWriteTimeUtc(zdjecie.Sciezka);using(var zapis=new FileStream(zdjecie.Sciezka,FileMode.Append,FileAccess.Write))zapis.WriteByte(0);File.SetLastWriteTimeUtc(zdjecie.Sciezka,dataPliku);var innyRozmiar=MetadaneInformacji.Pobierz(zdjecie,CancellationToken.None);Oczekuj(()=>innyRozmiar.IsCompleted);TestyOperacji.Wymagaj(!Object.ReferenceEquals(zmienione.Result,innyRozmiar.GetAwaiter().GetResult()),"Zmiana rozmiaru bez zmiany daty unieważnia cache");
        wyborWidoku.SelectedItem="Duże kafelki";PoczekajNaPanel();TestyOperacji.Wymagaj(TekstPanelu().Contains("Megapiksele"),"Panel działa niezależnie w Ikonach");Renderuj(Okno,Path.Combine(zrzuty,"31-panel-zdjecie-ikony.png"));
        PokazSzczegoly(zdjecie);var staryToken=anulowanieInformacji.Token;var audio=ostatnie.First(w=>w.Nazwa=="audio.wav");PokazSzczegoly(audio);PoczekajNaPanel();
        TestyOperacji.Wymagaj(staryToken.IsCancellationRequested&&TekstPanelu().Contains("audio.wav")&&!TekstPanelu().Contains("Megapiksele")&&TekstPanelu().Contains("Czas trwania"),"Zmiana zaznaczenia anuluje stare wyniki; metadane WAV");
        using(var anuluj=new CancellationTokenSource()){anuluj.Cancel();var zadanie=MetadaneInformacji.Pobierz(audio,anuluj.Token);Oczekuj(()=>zadanie.IsCompleted);TestyOperacji.Wymagaj(zadanie.IsCanceled,"Anulowany odczyt metadanych nie rozpoczyna pracy");}
        var film=ostatnie.First(w=>w.Nazwa=="film.avi");PokazSzczegoly(film);PoczekajNaPanel();TestyOperacji.Wymagaj(TekstPanelu().Contains("Czas trwania")&&TekstPanelu().Contains("Szerokość filmu"),"Poprawny AVI: czas i rozdzielczość");Renderuj(Okno,Path.Combine(zrzuty,"32-panel-film.png"));
        PokazSzczegoly(ostatnie.First(w=>w.Nazwa=="exif.jpg"));PoczekajNaPanel();TestyOperacji.Wymagaj(TekstPanelu().Contains("Aparat testowy"),"EXIF odczytany po zaznaczeniu JPEG");
        wyborWidoku.SelectedItem="Lista szczegółowa";lista.SelectedItems.Clear();lista.SelectedItems.Add(zdjecie);lista.SelectedItems.Add(audio);PokazInformacjeZaznaczenia();
        TestyOperacji.Wymagaj(TekstPanelu().Contains("2 elementów")&&TekstPanelu().Contains("Łączny znany rozmiar"),"Multi-select w Szczegółach");
        wyborWidoku.SelectedItem="Duże kafelki";TestyOperacji.Wymagaj(TekstPanelu().Contains("2 elementów"),"Multi-select w Ikonach");
        var klawisze=new byte[256];StanKlawiszy(klawisze);var ctrl=(byte[])klawisze.Clone();ctrl[0x11]=128;ctrl[0xA2]=128;UstawStanKlawiszy(ctrl);
        try{Oczekuj(()=>(System.Windows.Input.Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Control)!=0);ZaznaczKafelek(audio);TestyOperacji.Wymagaj(zaznaczone.Count==1&&lista.SelectedItems.Count==1,"Ctrl+klik usuwa wybór także z ukrytej listy");ZaznaczKafelek(audio);TestyOperacji.Wymagaj(zaznaczone.Count==2&&lista.SelectedItems.Count==2&&TekstPanelu().Contains("2 elementów"),"Ctrl+klik synchronizuje multi-select i panel");}finally{UstawStanKlawiszy(klawisze);}
        Renderuj(Okno,Path.Combine(zrzuty,"33-panel-multi-select.png"));
        var panel=Kontrolka<Border>("PanelSzczegolow");var separator=Kontrolka<GridSplitter>("SeparatorInformacji");Okno.UpdateLayout();double przed=panel.ActualWidth;
        separator.RaiseEvent(new DragStartedEventArgs(0,0){RoutedEvent=Thumb.DragStartedEvent});separator.RaiseEvent(new DragDeltaEventArgs(-40,0){RoutedEvent=Thumb.DragDeltaEvent});separator.RaiseEvent(new DragCompletedEventArgs(-40,0,false){RoutedEvent=Thumb.DragCompletedEvent});Okno.UpdateLayout();
        TestyOperacji.Wymagaj(Math.Abs(panel.ActualWidth-przed)>10&&Math.Abs(Kafelki.Ustawienia.SzerokoscPanelu-panel.ActualWidth)<2,"Resize rzeczywistym GridSplitter i zapis szerokości: przed="+przed+" po="+panel.ActualWidth+" zapis="+Kafelki.Ustawienia.SzerokoscPanelu);
        Kafelki.Wczytaj();TestyOperacji.Wymagaj(Math.Abs(Kafelki.Ustawienia.SzerokoscPanelu-panel.ActualWidth)<2,"Szerokość odtworzona z JSON");Renderuj(Okno,Path.Combine(zrzuty,"34-panel-resize.png"));
        var sekcja=szczegoly.Children.OfType<Expander>().First();sekcja.IsExpanded=false;Kafelki.Wczytaj();PokazInformacjeZaznaczenia();TestyOperacji.Wymagaj(!szczegoly.Children.OfType<Expander>().First().IsExpanded,"Pamięć zwiniętych sekcji");
        Kontrolka<Button>("ZamknijInformacje").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TestyOperacji.Wymagaj(panel.Visibility==Visibility.Collapsed&&!Kafelki.Ustawienia.PanelSzczegolow,"X wyłącza panel niezależnie od widoku");
        Kontrolka<Button>("MenuWyswietl").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var menu=Kontrolka<Button>("MenuWyswietl").ContextMenu;
        var przelacznik=menu.Items.OfType<MenuItem>().First(m=>Convert.ToString(m.Header)=="Panel informacji");przelacznik.IsChecked=true;przelacznik.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));menu.IsOpen=false;TestyOperacji.Wymagaj(panel.Visibility==Visibility.Visible,"ON w menu ponownie pokazuje panel");
        Kontrolka<Button>("InformacjeFolderu").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Oczekuj(()=>TekstPanelu().Contains("Rozmiar bezpośredni")&&!TekstPanelu().Contains("Ładowanie"));
        TestyOperacji.Wymagaj(TekstPanelu().Contains("Bezpośrednie pliki")&&TekstPanelu().Contains("Zawartość bezpośrednia")&&szczegoly.Children.OfType<Expander>().Any(e=>Convert.ToString(e.Header)=="NAJWIĘKSZE ELEMENTY"),"Folder spoza indeksu: rozmiar bezpośredni, struktura i największe pliki");Renderuj(Okno,Path.Combine(zrzuty,"35-panel-folder.png"));
        TestyOperacji.Wymagaj(Skaner.LiczbaSkanow==skany&&Skaner.LiczbaHashowan==hashe,"Zaznaczanie i panel nie skanują i nie hashują");
        var dysk=DaneDysku(Path.GetPathRoot(katalog));TestyOperacji.Wymagaj(dysk.ContainsKey("Pojemność")&&dysk.ContainsKey("System plików")&&dysk.ContainsKey("Etykieta"),"Dane dysku bez skanowania zawartości");
        Kafelki.Ustawienia.SekcjePanelu["PODSTAWOWE"]=true;PokazInformacjeZaznaczenia();
    }
}
}
