using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MapaDyskow {
public static class TestyWidokow {
    public static void Uruchom(List<string> wyniki){
        var pierwszy=new Wiersz {Nazwa="Film 2",Sciezka="Film 2.mp4",Rodzaj="plik",Rozmiar=500000000,Zmiana=200,Utworzenie=new DateTime(2025,1,1),Pliki=2,Podfoldery=2,Kopie=2,MetadanePodgladu=new MetadaneKafelka {Szerokosc=100,Wysokosc=100,Czas=TimeSpan.FromSeconds(20)}};
        var drugi=new Wiersz {Nazwa="Film 10",Sciezka="Film 10.mkv",Rodzaj="plik",Rozmiar=20000000000,Zmiana=300,Utworzenie=new DateTime(2026,1,1),Pliki=10,Podfoldery=10,Kopie=10,MetadanePodgladu=new MetadaneKafelka {Szerokosc=1000,Wysokosc=1000,Czas=TimeSpan.FromSeconds(100)}};
        foreach(string pole in new[]{"Nazwa","Rozmiar","Zmiana","Utworzenie","Pliki","Podfoldery","Kopie","Wymiary","Czas"}){TestyOperacji.Wymagaj(SortowanieElementow.Porownaj(pierwszy,drugi,pole,false,false)<0,"Rosnąco: "+pole);TestyOperacji.Wymagaj(SortowanieElementow.Porownaj(pierwszy,drugi,pole,true,false)>0,"Malejąco: "+pole);}
        TestyOperacji.Wymagaj(SortowanieElementow.Porownaj(pierwszy,drugi,"TypTekst",false,false)<0,"Jednakowy typ ma stabilną naturalną kolejność");
        TestyOperacji.Wymagaj(SortowanieElementow.Porownaj(pierwszy,drugi,"Rozszerzenie",false,false)>0,"MP4 po MKV");
        var folder=new Wiersz {Nazwa="Z folder",Sciezka="Z folder",Rodzaj="folder",Rozmiar=1};
        foreach(string pole in new[]{"Nazwa","Rozmiar","Zmiana"})foreach(bool malejaco in new[]{false,true})TestyOperacji.Wymagaj(SortowanieElementow.Porownaj(folder,pierwszy,pole,malejaco,true)<0,"Folder zawsze pierwszy");
        TestyOperacji.Wymagaj(SortowanieElementow.Porownaj(folder,pierwszy,"Nazwa",false,false)>0&&folder.Rozszerzenie=="","Folder mieszany i pusty format");
        TestyOperacji.Wymagaj(SortowanieElementow.SkalaPoKole(100,120)==105&&SortowanieElementow.SkalaPoKole(100,-120)==95&&SortowanieElementow.SkalaPoKole(140,120)==140&&SortowanieElementow.SkalaPoKole(70,-120)==70,"Krok koła i limity");
        wyniki.Add("Widoki: naturalne A-Z/Z-A, format, typ, liczby, daty, wymiary, czas, foldery, krok skali i limity: OK");
    }
}
public sealed partial class OknoGlowne {
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetKeyboardState")]static extern bool StanKlawiszy(byte[] stan);
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetKeyboardState")]static extern bool UstawStanKlawiszy(byte[] stan);
    public void TestujWidoki(string zrzuty){
        var dane=Enumerable.Range(1,500).Select(i=>new Wiersz {Nazwa="Film "+i,Sciezka="fixture-"+i+".mp4",Rodzaj="plik",Rozmiar=i}).ToList();
        ostatnie=dane;lista.ItemsSource=dane;wyborWidoku.SelectedItem="Lista szczegółowa";Okno.UpdateLayout();lista.SelectedItem=dane[42];lista.SelectedItems.Add(dane[43]);
        wyborWidoku.SelectedItem="Duże kafelki";Okno.UpdateLayout();TestyOperacji.Wymagaj(zaznaczone.Contains(dane[42])&&zaznaczone.Contains(dane[43]),"Zaznaczenie szczegóły → ikony");
        var scroll=ScrollWidoku();scroll.ScrollToVerticalOffset(15);Renderuj(Okno,Path.Combine(zrzuty,"20-ikony-100.png"));var kotwicaPrzed=KotwicaWidoczna();
        skalaKafelkow.Value=105;Renderuj(Okno,Path.Combine(zrzuty,"21-ikony-105.png"));var kotwicaPo=KotwicaWidoczna();TestyOperacji.Wymagaj(Math.Abs(dane.IndexOf(kotwicaPrzed)-dane.IndexOf(kotwicaPo))<12,"Kotwica podczas skalowania");
        TestyOperacji.Wymagaj(ukladBiezacy.Skala==105&&Kafelki.Ustawienia.SkalaDuzych==105&&opisSkali.Text=="105%","Synchronizacja wspólnego suwaka");
        skalaKafelkow.Value=100;var klawisze=new byte[256];StanKlawiszy(klawisze);var ctrl=(byte[])klawisze.Clone();ctrl[0x11]=128;ctrl[0xA2]=128;UstawStanKlawiszy(ctrl);
        bool ctrlDostepny=false;
        try{Oczekuj(()=>{ctrlDostepny=(Keyboard.Modifiers&ModifierKeys.Control)!=0;return ctrlDostepny;});var kolo=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=UIElement.PreviewMouseWheelEvent};kafelki.RaiseEvent(kolo);TestyOperacji.Wymagaj(kolo.Handled&&skalaKafelkow.Value==105&&ukladBiezacy.Skala==105,"Ctrl+koło w rzeczywistym WPF z stanem Ctrl systemu");var wDol=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent};kafelki.RaiseEvent(wDol);TestyOperacji.Wymagaj(wDol.Handled&&skalaKafelkow.Value==100,"Ctrl+koło w dół");}finally{UstawStanKlawiszy(klawisze);}
        foreach(int skala in new[]{70,75,90,110,140}){skalaKafelkow.Value=skala;Renderuj(Okno,Path.Combine(zrzuty,"22-ikony-"+skala+".png"));}
        wyborWidoku.SelectedItem="Lista szczegółowa";Renderuj(Okno,Path.Combine(zrzuty,"23-szczegoly.png"));TestyOperacji.Wymagaj(lista.SelectedItems.Count==2,"Zaznaczenie ikony → szczegóły");
        ostatnie=Enumerable.Range(1,100000).Select(i=>new Wiersz {Nazwa="Rekord "+i,Sciezka="fixture-"+i,Rodzaj="folder"}).ToList();lista.ItemsSource=ostatnie;Okno.UpdateLayout();TestyOperacji.Wymagaj(Potomkowie(lista).OfType<DataGridRow>().Count()<100&&lista.EnableRowVirtualization&&lista.EnableColumnVirtualization,"100000 modeli, mniej niż 100 kontrolek tabeli");
        var nazwa=lista.Columns[0];nazwa.Width=333;nazwa.DisplayIndex=2;lista.Columns[6].Visibility=Visibility.Visible;ZapiszKolumny();Kafelki.Zapisz();Kafelki.Wczytaj();ZbudujSzczegoly();Okno.UpdateLayout();
        nazwa=lista.Columns.First(k=>k.SortMemberPath=="Nazwa");TestyOperacji.Wymagaj(Math.Abs(nazwa.Width.Value-333)<1&&nazwa.DisplayIndex==2&&lista.Columns[6].Visibility==Visibility.Visible,"Trwałość szerokości, kolejności i widoczności");
        var zdarzenie=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=UIElement.PreviewMouseWheelEvent};kafelki.RaiseEvent(zdarzenie);TestyOperacji.Wymagaj(!zdarzenie.Handled,"Koło bez Ctrl nie skaluje");
        File.WriteAllText(Path.Combine(zrzuty,"test-widokow.txt"),"100000 modeli; tabela zwirtualizowana; wybór zachowany; kotwica skali zachowana; szerokości, kolejność i widoczność odtworzone z JSON. Koło bez Ctrl nie jest przechwytywane. Ctrl+koło: rzeczywiste okno WPF, stan klawisza Ctrl w wątku WPF, zdarzenie koła WPF w górę i w dół. Test automatyczny nie potwierdza fizycznej myszy ani odczuwanej płynności.");
        var nazwaSort=lista.Columns.First(k=>k.SortMemberPath=="Nazwa");
        typeof(DataGrid).GetMethod("OnSorting",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(lista,new object[]{new DataGridSortingEventArgs(nazwaSort)});
        Oczekuj(()=>wczytanaWersja==wersja);bool kierunek=ukladBiezacy.Malejaco;
        typeof(DataGrid).GetMethod("OnSorting",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(lista,new object[]{new DataGridSortingEventArgs(nazwaSort)});
        Oczekuj(()=>wczytanaWersja==wersja);TestyOperacji.Wymagaj(kierunek!=ukladBiezacy.Malejaco&&nazwaSort.SortDirection.HasValue,"Nagłówek przełącza kierunek");
        var stanUkladu=Program.Json.Deserialize<Wyglad>(File.ReadAllText(Path.Combine(Program.Katalog,"wyglad.json")));TestyOperacji.Wymagaj(stanUkladu.UkladyFolderow[KluczWidoku()].Sortowanie=="Nazwa"&&stanUkladu.UkladyFolderow[KluczWidoku()].Malejaco==ukladBiezacy.Malejaco,"Trwałość sortowania folderu");
        using(var baza=new Baza(Program.Indeks,false)){var wszystkie=ZawartoscFolderu(baza,folder,"",true,1000,0);var pierwsza=ZawartoscFolderu(baza,folder,"",true,20,0);var druga=ZawartoscFolderu(baza,folder,"",true,20,20);TestyOperacji.Wymagaj(pierwsza.Concat(druga).Select(w=>w.Sciezka).SequenceEqual(wszystkie.Take(40).Select(w=>w.Sciezka)),"Sortowanie przed podziałem na strony");}
        Renderuj(Okno,Path.Combine(zrzuty,"24-szczegoly-sortowanie.png"));
    }
}
}
