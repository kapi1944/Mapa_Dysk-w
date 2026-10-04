using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace MapaDyskow {
public sealed class UkladKolumn {
    public string Pole {get;set;} public double Szerokosc {get;set;} public int Kolejnosc {get;set;} public bool Widoczna {get;set;}
}
public sealed class UkladFolderu {
    public string Sortowanie {get;set;} public bool Malejaco {get;set;} public double Skala {get;set;}
    public UkladFolderu(){Sortowanie="Nazwa";Skala=100;}
}
public static class SortowanieElementow {
    [DllImport("shlwapi.dll",CharSet=CharSet.Unicode,EntryPoint="StrCmpLogicalW")] static extern int PorownajNazwy(string pierwsza,string druga);
    public static int Porownaj(Wiersz pierwszy,Wiersz drugi,string pole,bool malejaco,bool foldery){
        if(foldery&&(pierwszy.Rodzaj=="folder")!=(drugi.Rodzaj=="folder"))return pierwszy.Rodzaj=="folder"?-1:1;
        int wynik;
        switch(pole){
            case "Rozmiar":wynik=pierwszy.Rozmiar.CompareTo(drugi.Rozmiar);break;
            case "Zmiana":wynik=pierwszy.Zmiana.CompareTo(drugi.Zmiana);break;
            case "Utworzenie":wynik=Nullable.Compare(pierwszy.Utworzenie,drugi.Utworzenie);break;
            case "Pliki":wynik=pierwszy.Pliki.CompareTo(drugi.Pliki);break;
            case "Podfoldery":wynik=pierwszy.Podfoldery.CompareTo(drugi.Podfoldery);break;
            case "Kopie":wynik=pierwszy.Kopie.CompareTo(drugi.Kopie);break;
            case "Wymiary":wynik=pierwszy.Piksele.CompareTo(drugi.Piksele);break;
            case "Czas":wynik=pierwszy.Sekundy.CompareTo(drugi.Sekundy);break;
            case "TypTekst":wynik=PorownajNazwy(pierwszy.TypTekst,drugi.TypTekst);break;
            case "Rozszerzenie":wynik=PorownajNazwy(pierwszy.Rozszerzenie,drugi.Rozszerzenie);break;
            case "Sciezka":wynik=PorownajNazwy(pierwszy.Sciezka,drugi.Sciezka);break;
            case "StatusDuplikatu":wynik=PorownajNazwy(pierwszy.StatusDuplikatu,drugi.StatusDuplikatu);break;
            case "Oznaczenia":wynik=PorownajNazwy(pierwszy.Oznaczenia??"",drugi.Oznaczenia??"");break;
            default:wynik=PorownajNazwy(pierwszy.Nazwa,drugi.Nazwa);break;
        }
        if(wynik==0)wynik=PorownajNazwy(pierwszy.Sciezka,drugi.Sciezka);
        return malejaco?-Math.Sign(wynik):Math.Sign(wynik);
    }
    public static double SkalaPoKole(double skala,int delta){return Math.Max(70,Math.Min(140,skala+Math.Sign(delta)*5));}
}
public sealed partial class OknoGlowne {
    List<Wiersz> Pobierz(string wybranyTryb,string wybranyFolder,string filtr,bool pokaz,int numerStrony){
        int limit=Program.Ustawienia.rozmiarStrony;
        UkladFolderu uklad;string klucz=wybranyTryb=="Folder"?wybranyFolder:"sekcja:"+wybranyTryb;
        if(wybranyTryb=="Folder"||!Kafelki.Ustawienia.UkladyFolderow.TryGetValue(klucz,out uklad)||wybranyTryb=="Potwierdzone duplikaty"||wybranyTryb=="Potencjalnie odzyskiwane miejsce")return UzupelnijStrone(PobierzSurowe(wybranyTryb,wybranyFolder,filtr,pokaz,numerStrony,limit));
        var dane=PobierzSurowe(wybranyTryb,wybranyFolder,filtr,pokaz,0,int.MaxValue-1);
        string pole=uklad.Sortowanie;bool malejaco=uklad.Malejaco,foldery=Kafelki.Ustawienia.FolderyNaPoczatku;UzupelnijDaneSortowania(dane,pole);
        dane.Sort((pierwszy,drugi)=>SortowanieElementow.Porownaj(pierwszy,drugi,pole,malejaco,foldery));
        return UzupelnijStrone(dane.Skip(numerStrony*limit).Take(limit+1).ToList());
    }
    List<Wiersz> UzupelnijStrone(List<Wiersz> dane){using(var baza=new Baza(Program.Indeks,false))UzupelnijKopie(baza,dane);return dane;}
    void UzupelnijDaneSortowania(List<Wiersz> dane,string pole){
        if(pole=="Utworzenie")UzupelnijDatySortowania(dane);
        if(pole=="Kopie"||pole=="StatusDuplikatu")using(var baza=new Baza(Program.Indeks,false))UzupelnijKopie(baza,dane);
        if(pole=="Wymiary"||pole=="Czas")foreach(var element in dane.Where(w=>w.Rodzaj=="plik")){element.MetadanePodgladu=Kafelki.Metadane(element,System.Threading.CancellationToken.None).GetAwaiter().GetResult();}
    }
    static void UzupelnijDatySortowania(IEnumerable<Wiersz> dane){foreach(var element in dane){try{FileSystemInfo wpis=element.Rodzaj=="folder"?(FileSystemInfo)new DirectoryInfo(element.Sciezka):new FileInfo(element.Sciezka);if(wpis.Exists&&(wpis.Attributes&(FileAttributes.Offline|FileAttributes.ReparsePoint))==0&&(((int)wpis.Attributes)&0x440000)==0)element.Utworzenie=wpis.CreationTime;}catch(IOException){}catch(UnauthorizedAccessException){}}}
    int wersjaPanelu;bool budowanieKolumn;UkladFolderu ukladBiezacy=new UkladFolderu();
    string[] polaKolumn={"Nazwa","TypTekst","Rozszerzenie","Rozmiar","Utworzenie","Zmiana","Sciezka","Kopie","StatusDuplikatu","Oznaczenia","Wymiary","Czas","Pliki","Podfoldery"};
    string[] nazwyKolumn={"Nazwa","Typ","Format","Rozmiar","Data utworzenia","Data modyfikacji","Lokalizacja","Liczba kopii","Status duplikatu","Tagi","Wymiary","Czas trwania","Liczba plików","Liczba folderów"};
    string[] tekstKolumn={"Nazwa","TypTekst","Rozszerzenie","RozmiarTekst","UtworzenieTekst","DataTekst","Sciezka","Kopie","StatusDuplikatu","TagiKrotkie","WymiaryTekst","CzasTekst","PlikiTekst","PodfolderyTekst"};
    void WczytajUklad(){UkladFolderu zapisany;ukladBiezacy=Kafelki.Ustawienia.UkladyFolderow.TryGetValue(KluczWidoku(),out zapisany)?zapisany:new UkladFolderu {Sortowanie=tryb=="Folder"?"Nazwa":"",Malejaco=tryb!="Folder"};}
    void ZapiszUklad(){Kafelki.Ustawienia.UkladyFolderow[KluczWidoku()]=ukladBiezacy;Kafelki.Zapisz();}
    void ZbudujSzczegoly(){
        budowanieKolumn=true;lista.Columns.Clear();lista.RowHeight=30;var stylWiersza=new Style(typeof(DataGridRow));stylWiersza.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Oznaczenia")));lista.RowStyle=stylWiersza;lista.CanUserReorderColumns=true;lista.CanUserResizeColumns=true;
        for(int i=0;i<polaKolumn.Length;i++){
            string pole=polaKolumn[i];var zapis=Kafelki.Ustawienia.Kolumny.FirstOrDefault(k=>k.Pole==pole);
            DataGridColumn kolumna;
            if(i==0){var szablon=(DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:lokalne='clr-namespace:MapaDyskow;assembly=MapaDyskow'><StackPanel Orientation='Horizontal' ToolTip='{Binding Sciezka}'><Grid Width='28' VerticalAlignment='Center'><lokalne:IkonaPliku Element='{Binding}' Width='20' Height='20' SnapsToDevicePixels='True'/></Grid><StackPanel VerticalAlignment='Center'><TextBlock Text='{Binding Nazwa}' TextTrimming='CharacterEllipsis'/></StackPanel></StackPanel></DataTemplate>");kolumna=new DataGridTemplateColumn {CellTemplate=szablon};}
            else kolumna=new DataGridTextColumn {Binding=new Binding(tekstKolumn[i])};
            kolumna.Header=nazwyKolumn[i];kolumna.SortMemberPath=pole;kolumna.CanUserSort=true;kolumna.MinWidth=55;kolumna.Width=zapis==null?(i==0?240:i==1?85:i==2?70:i==3?95:140):Math.Max(55,zapis.Szerokosc);kolumna.Visibility=zapis==null?(i<6?Visibility.Visible:Visibility.Collapsed):(zapis.Widoczna?Visibility.Visible:Visibility.Collapsed);lista.Columns.Add(kolumna);
            DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty,typeof(DataGridColumn)).AddValueChanged(kolumna,(s,e)=>ZapiszKolumny());
        }
        foreach(var zapis in Kafelki.Ustawienia.Kolumny.OrderBy(k=>k.Kolejnosc)){var kolumna=lista.Columns.FirstOrDefault(k=>k.SortMemberPath==zapis.Pole);if(kolumna!=null)kolumna.DisplayIndex=Math.Max(0,Math.Min(lista.Columns.Count-1,zapis.Kolejnosc));}
        budowanieKolumn=false;StrzalkiSortowania();
    }
    void ZapiszKolumny(){if(budowanieKolumn||WidokDuplikatow())return;Kafelki.Ustawienia.Kolumny=lista.Columns.Select(k=>new UkladKolumn {Pole=k.SortMemberPath,Szerokosc=k.ActualWidth>0?k.ActualWidth:k.Width.Value,Kolejnosc=k.DisplayIndex,Widoczna=k.Visibility==Visibility.Visible}).ToList();zapisSkali.Stop();zapisSkali.Start();}
    void StrzalkiSortowania(){foreach(var kolumna in lista.Columns)kolumna.SortDirection=kolumna.SortMemberPath==ukladBiezacy.Sortowanie?(ukladBiezacy.Malejaco?ListSortDirection.Descending:ListSortDirection.Ascending):(ListSortDirection?)null;}
    void UstawSortowanie(string pole,bool? malejaco=null){ukladBiezacy.Malejaco=malejaco??(ukladBiezacy.Sortowanie==pole&&!ukladBiezacy.Malejaco);ukladBiezacy.Sortowanie=pole;ZapiszUklad();ZapamietajStan();OdswiezListe();}
    ContextMenu MenuKolumn(){var menu=new ContextMenu();foreach(var kolumna in lista.Columns){var pozycja=new MenuItem {Header=kolumna.Header,IsCheckable=true,IsChecked=kolumna.Visibility==Visibility.Visible};pozycja.Click+=(s,e)=>{kolumna.Visibility=pozycja.IsChecked?Visibility.Visible:Visibility.Collapsed;ZapiszKolumny();foreach(var wiersz in Potomkowie(lista).OfType<DataGridRow>())UzupelnijWiersz(wiersz);};menu.Items.Add(pozycja);}return menu;}
    void PrzygotujSzczegoly(){
        lista.LoadingRow+=(s,e)=>UzupelnijWiersz(e.Row);
        lista.Sorting+=(s,e)=>{if(WidokDuplikatow())return;e.Handled=true;UstawSortowanie(e.Column.SortMemberPath);};
        lista.ColumnReordered+=(s,e)=>ZapiszKolumny();
        lista.AddHandler(UIElement.PreviewMouseRightButtonDownEvent,new MouseButtonEventHandler((s,e)=>{var naglowek=Potomkowie(lista).OfType<DataGridColumnHeader>().FirstOrDefault(k=>k.IsMouseOver);if(naglowek!=null){e.Handled=true;var menu=MenuKolumn();menu.PlacementTarget=naglowek;menu.IsOpen=true;}}));
        lista.SelectionChanged+=(s,e)=>{if(lista.Visibility!=Visibility.Visible)return;zaznaczone=new HashSet<Wiersz>(lista.SelectedItems.Cast<Wiersz>());PokazInformacjeZaznaczenia();};
        var wyswietl=Kontrolka<Button>("MenuWyswietl");wyswietl.Click+=(s,e)=>{
            var menu=new ContextMenu();string[] nazwy={"Bardzo duże ikony","Duże ikony","Średnie ikony","Małe ikony"};double[] skale={140,110,90,75};
            for(int i=0;i<nazwy.Length;i++){double skala=skale[i];var pozycja=new MenuItem {Header=nazwy[i]};pozycja.Click+=(a,b)=>{wyborWidoku.SelectedItem="Duże kafelki";skalaKafelkow.Value=skala;};menu.Items.Add(pozycja);}
            menu.Items.Add(new Separator());foreach(string nazwa in new[]{"Szczegóły","Kompaktowe kafelki"}){var pozycja=new MenuItem {Header=nazwa};pozycja.Click+=(a,b)=>wyborWidoku.SelectedItem=nazwa=="Szczegóły"?"Lista szczegółowa":nazwa;menu.Items.Add(pozycja);}
            menu.Items.Add(new Separator());var panel=new MenuItem {Header="Panel informacji",IsCheckable=true,IsChecked=Kafelki.Ustawienia.PanelSzczegolow};panel.Click+=(a,b)=>{Kafelki.Ustawienia.PanelSzczegolow=panel.IsChecked;PokazPanelSzczegolow();Kafelki.Zapisz();};menu.Items.Add(panel);wyswietl.ContextMenu=menu;menu.PlacementTarget=wyswietl;menu.IsOpen=true;
        };
        var sortuj=Kontrolka<Button>("MenuSortuj");sortuj.Click+=(s,e)=>{var menu=new ContextMenu();for(int i=0;i<6;i++){string pole=polaKolumn[i];var pozycja=new MenuItem {Header=nazwyKolumn[i],IsCheckable=true,IsChecked=ukladBiezacy.Sortowanie==pole};pozycja.Click+=(a,b)=>UstawSortowanie(pole,false);menu.Items.Add(pozycja);}menu.Items.Add(new Separator());foreach(bool malejaco in new[]{false,true}){var pozycja=new MenuItem {Header=malejaco?"Malejąco":"Rosnąco",IsCheckable=true,IsChecked=ukladBiezacy.Malejaco==malejaco};pozycja.Click+=(a,b)=>UstawSortowanie(ukladBiezacy.Sortowanie,malejaco);menu.Items.Add(pozycja);}var foldery=new MenuItem {Header="Foldery zawsze na początku",IsCheckable=true,IsChecked=Kafelki.Ustawienia.FolderyNaPoczatku};foldery.Click+=(a,b)=>{Kafelki.Ustawienia.FolderyNaPoczatku=foldery.IsChecked;UstawSortowanie(ukladBiezacy.Sortowanie,ukladBiezacy.Malejaco);};menu.Items.Add(foldery);menu.PlacementTarget=sortuj;menu.IsOpen=true;};
        kafelki.PreviewMouseWheel+=(s,e)=>{if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;e.Handled=true;skalaKafelkow.Value=SortowanieElementow.SkalaPoKole(skalaKafelkow.Value,e.Delta);};
        PrzygotujPanelInformacji();PokazPanelSzczegolow();
    }
    async void UzupelnijWiersz(DataGridRow wiersz){
        var element=wiersz.Item as Wiersz;if(element==null||element.Rodzaj!="plik"||!lista.Columns.Any(k=>k.Visibility==Visibility.Visible&&(k.SortMemberPath=="Wymiary"||k.SortMemberPath=="Czas")))return;
        try{element.MetadanePodgladu=element.MetadanePodgladu??await Kafelki.Metadane(element,System.Threading.CancellationToken.None);if(!Object.ReferenceEquals(wiersz.Item,element))return;foreach(var tekst in Potomkowie(wiersz).OfType<TextBlock>()){var powiazanie=tekst.GetBindingExpression(TextBlock.TextProperty);if(powiazanie!=null)powiazanie.UpdateTarget();}}catch(IOException){}catch(UnauthorizedAccessException){}
    }
    async void UzupelnijPanel(Wiersz element){int numerPanelu=++wersjaPanelu;
        if(!Kafelki.Ustawienia.PanelSzczegolow||element.Rodzaj!="plik")return;
        try{var dane=element.MetadanePodgladu??await Kafelki.Metadane(element,System.Threading.CancellationToken.None);element.MetadanePodgladu=dane;element.Utworzenie=dane.Utworzenie;if(numerPanelu!=wersjaPanelu||lista.SelectedItem!=element)return;
            if(dane.Miniatura!=null)szczegoly.Children.Insert(0,new Image {Source=dane.Miniatura,MaxHeight=200,Stretch=System.Windows.Media.Stretch.Uniform,Margin=new Thickness(0,0,0,12)});
            DodajSzczegol("Typ",element.TypTekst);DodajSzczegol("Format",element.Rozszerzenie);DodajSzczegol("Data utworzenia",element.UtworzenieTekst);DodajSzczegol("Liczba kopii",element.Kopie.ToString());
            if(element.Piksele>0)DodajSzczegol("Wymiary",element.WymiaryTekst);if(element.Sekundy>0)DodajSzczegol("Czas trwania",element.CzasTekst);
            if(dane.Klatki>0)DodajSzczegol("FPS",(dane.Klatki/1000.0).ToString("0.###"));if(dane.Bitrate>0)DodajSzczegol("Bitrate",(dane.Bitrate/1000.0).ToString("0")+" kb/s");if(dane.Czestotliwosc>0)DodajSzczegol("Sample rate",dane.Czestotliwosc+" Hz");
        }catch(IOException){}catch(UnauthorizedAccessException){}
    }
    Wiersz KotwicaWidoczna(){if(lista.Visibility==Visibility.Visible){var wiersz=Potomkowie(lista).OfType<DataGridRow>().FirstOrDefault(r=>r.IsVisible&&r.TransformToAncestor(lista).Transform(new Point()).Y>=0);return wiersz==null?lista.SelectedItem as Wiersz:wiersz.Item as Wiersz;}var scroll=ScrollWidoku();var rzedy=kafelki.ItemsSource as List<DaneRzedu>;if(scroll==null||rzedy==null||rzedy.Count==0)return null;int indeks=Math.Max(0,Math.Min(rzedy.Count-1,(int)scroll.VerticalOffset));return rzedy[indeks].Elementy.FirstOrDefault();}
    void PrzywrocKotwice(Wiersz element){if(element==null)return;Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(lista.Visibility==Visibility.Visible)lista.ScrollIntoView(element);else{var rzedy=kafelki.ItemsSource as List<DaneRzedu>;if(rzedy==null)return;int indeks=rzedy.FindIndex(r=>r.Elementy.Contains(element));var scroll=ScrollWidoku();if(scroll!=null&&indeks>=0)scroll.ScrollToVerticalOffset(indeks);}}));}
}
}
