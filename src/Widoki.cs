using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace MapaDyskow {
public sealed partial class OknoGlowne {
    ListBox kafelki;ComboBox wyborWidoku;HashSet<Wiersz> zaznaczone=new HashSet<Wiersz>();Wiersz kotwica;bool zmianaWidoku;
    Window oknoPersonalizacji;Slider skalaKafelkow;TextBlock opisSkali;System.Windows.Threading.DispatcherTimer zapisSkali;
    void OtworzPersonalizacje(){if(oknoPersonalizacji!=null){oknoPersonalizacji.Activate();return;}oknoPersonalizacji=new OknoWygladu(Okno,PokazWybranyWidok).Okno;oknoPersonalizacji.Closed+=(s,e)=>oknoPersonalizacji=null;oknoPersonalizacji.Show();}
    void PrzygotujKafelki(){
        Kafelki.Wczytaj();wyborWidoku=Kontrolka<ComboBox>("TrybWidoku");wyborWidoku.ItemsSource=new[]{"Duże kafelki","Kompaktowe kafelki","Lista szczegółowa"};
        kafelki=Kontrolka<ListBox>("Kafelki");VirtualizingPanel.SetIsVirtualizing(kafelki,true);VirtualizingPanel.SetVirtualizationMode(kafelki,VirtualizationMode.Recycling);ScrollViewer.SetCanContentScroll(kafelki,true);
        var fabryka=new FrameworkElementFactory(typeof(RzadKafelkow));kafelki.ItemTemplate=new DataTemplate {VisualTree=fabryka};var styl=new Style(typeof(ListBoxItem));styl.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));styl.Setters.Add(new Setter(Control.MarginProperty,new Thickness(0)));styl.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Left));var otoczka=new FrameworkElementFactory(typeof(ContentPresenter));otoczka.SetBinding(ContentPresenter.ContentProperty,new Binding());otoczka.SetBinding(ContentPresenter.ContentTemplateProperty,new Binding("ContentTemplate"){RelativeSource=new RelativeSource(RelativeSourceMode.TemplatedParent)});styl.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(ListBoxItem)){VisualTree=otoczka}));kafelki.ItemContainerStyle=styl;
        wyborWidoku.SelectionChanged+=(s,e)=>{if(zmianaWidoku)return;Kafelki.Ustawienia.Widoki[KluczWidoku()]=Convert.ToString(wyborWidoku.SelectedItem);Kafelki.Zapisz();var kotwicaWidoku=KotwicaWidoczna();PokazWybranyWidok();PrzywrocKotwice(kotwicaWidoku);};kafelki.SizeChanged+=(s,e)=>{if(Math.Abs(e.NewSize.Width-e.PreviousSize.Width)>1)PokazWybranyWidok();};
        skalaKafelkow=Kontrolka<Slider>("SkalaKafelkow");opisSkali=Kontrolka<TextBlock>("OpisSkali");
        zapisSkali=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(350)};zapisSkali.Tick+=(s,e)=>{zapisSkali.Stop();Kafelki.Zapisz();};Okno.Closing+=(s,e)=>{if(zapisSkali.IsEnabled){zapisSkali.Stop();Kafelki.Zapisz();}};
        skalaKafelkow.ValueChanged+=(s,e)=>{if(zmianaWidoku)return;var kotwicaSkali=KotwicaWidoczna();double wartosc=Math.Round(e.NewValue);ukladBiezacy.Skala=wartosc;Kafelki.Ustawienia.UkladyFolderow[KluczWidoku()]=ukladBiezacy;if(Convert.ToString(wyborWidoku.SelectedItem)=="Kompaktowe kafelki")Kafelki.Ustawienia.SkalaKompaktowych=wartosc;else Kafelki.Ustawienia.SkalaDuzych=wartosc;PokazWybranyWidok();PrzywrocKotwice(kotwicaSkali);zapisSkali.Stop();zapisSkali.Start();};
        Kontrolka<Button>("Skala75").Click+=(s,e)=>skalaKafelkow.Value=75;Kontrolka<Button>("Skala100").Click+=(s,e)=>skalaKafelkow.Value=100;Kontrolka<Button>("Skala125").Click+=(s,e)=>skalaKafelkow.Value=125;
        Kontrolka<Button>("Wyglad").Click+=(s,e)=>OtworzPersonalizacje();lista.SelectionMode=DataGridSelectionMode.Extended;lista.SelectionUnit=DataGridSelectionUnit.FullRow;PrzygotujSzczegoly();

    }
    string KluczWidoku(){return tryb=="Folder"?folder:"sekcja:"+tryb;}
    void PokazKopie(Wiersz w){if(w.Rodzaj=="plik"&&w.Kopie>1&&!String.IsNullOrEmpty(w.Hash)){sekcje.SelectedItem="Potwierdzone duplikaty";szukaj.Text=w.Hash;strona=0;opoznienie.Stop();OdswiezListe();}else{lista.SelectedItem=w;PorownajWybrany();}}
    void PokazWybranyWidok(){
        if(kafelki==null)return;string widok;if(!Kafelki.Ustawienia.Widoki.TryGetValue(KluczWidoku(),out widok))widok="Lista szczegółowa";
        bool bylaLista=lista.Visibility==Visibility.Visible;zmianaWidoku=true;wyborWidoku.SelectedItem=widok;zmianaWidoku=false;bool listaWidoczna=widok=="Lista szczegółowa"||WidokDuplikatow();lista.Visibility=listaWidoczna?Visibility.Visible:Visibility.Collapsed;kafelki.Visibility=listaWidoczna?Visibility.Collapsed:Visibility.Visible;wyborWidoku.IsEnabled=!WidokDuplikatow();
        Kontrolka<WrapPanel>("SterowanieSkala").Visibility=listaWidoczna?Visibility.Collapsed:Visibility.Visible;
        if(bylaLista!=listaWidoczna)PokazInformacjeZaznaczenia();if(listaWidoczna){kafelki.ItemsSource=null;return;}bool kompaktowy=widok=="Kompaktowe kafelki";double skala=kompaktowy?Kafelki.Ustawienia.SkalaKompaktowych:Kafelki.Ustawienia.SkalaDuzych;UkladFolderu zapisany;if(!kompaktowy&&Kafelki.Ustawienia.UkladyFolderow.TryGetValue(KluczWidoku(),out zapisany))skala=zapisany.Skala;skala=Double.IsNaN(skala)?100:Math.Max(70,Math.Min(140,skala));zmianaWidoku=true;skalaKafelkow.Value=skala;opisSkali.Text=skala.ToString("0")+"%";zmianaWidoku=false;
        double dostepna=Math.Max(100,kafelki.ActualWidth-22),docelowa=(kompaktowy?160:210)*skala/100;int kolumny=Math.Max(1,(int)(dostepna/(docelowa+10)));double szerokosc=.85*(dostepna/kolumny-10)+.15*Math.Min(docelowa,dostepna/kolumny-10);var rzedy=new List<DaneRzedu>();for(int i=0;i<ostatnie.Count;i+=kolumny)rzedy.Add(new DaneRzedu {Elementy=ostatnie.GetRange(i,Math.Min(kolumny,ostatnie.Count-i)),Kompaktowy=kompaktowy,Szerokosc=szerokosc,Klik=ZaznaczKafelek,Otworz=OtworzElement,Kopie=PokazKopie,Zaznaczony=w=>zaznaczone.Contains(w)});kafelki.ItemsSource=rzedy;
    }
    void ZaznaczKafelek(Wiersz w){bool ctrl=(Keyboard.Modifiers&ModifierKeys.Control)!=0,shift=(Keyboard.Modifiers&ModifierKeys.Shift)!=0;if(!ctrl)zaznaczone.Clear();if(shift&&kotwica!=null){int a=ostatnie.IndexOf(kotwica),b=ostatnie.IndexOf(w);if(a>=0&&b>=0)foreach(var element in ostatnie.Skip(Math.Min(a,b)).Take(Math.Abs(b-a)+1))zaznaczone.Add(element);}else{if(ctrl&&zaznaczone.Contains(w))zaznaczone.Remove(w);else zaznaczone.Add(w);kotwica=w;}lista.SelectedItems.Clear();foreach(var element in zaznaczone)lista.SelectedItems.Add(element);PokazInformacjeZaznaczenia();OdswiezZaznaczenie(kafelki);stan.Text=zaznaczone.Count+" zaznaczonych elementów na bieżącej stronie";}
    void OdswiezZaznaczenie(DependencyObject element){var ramka=element as Border;if(ramka!=null&&ramka.Tag is Wiersz){bool wybrany=zaznaczone.Contains((Wiersz)ramka.Tag);ramka.BorderBrush=Program.Kolor(wybrany?"#7FB3DF":"#39434F");ramka.BorderThickness=new Thickness(wybrany?2:1);if(ramka.Resources.Contains("TloNormalne"))ramka.Background=wybrany?Program.Kolor("#F035485F"):(System.Windows.Media.Brush)ramka.Resources["TloNormalne"];}for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(element);i++)OdswiezZaznaczenie(System.Windows.Media.VisualTreeHelper.GetChild(element,i));}
    void OtworzElement(Wiersz w){if(w.Rodzaj=="folder")OtworzFolder(w.Sciezka);else if(w.Rodzaj=="duplikat")Porownaj(w);else{try{if(!File.Exists(w.Sciezka))throw new IOException("Plik nie jest dostępny w tej lokalizacji.");System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(w.Sciezka){UseShellExecute=true});}catch(Exception blad){MessageBox.Show(blad.Message,"Otwieranie pliku");}}}
    List<Wiersz> ZawartoscFolderu(Baza baza,string sciezka,string filtr,bool pokaz,int limit,int od){
        var wynik=EksploratorFolderow.Odczytaj(baza,sciezka).Where(w=>String.IsNullOrEmpty(filtr)||w.Nazwa.IndexOf(filtr,StringComparison.OrdinalIgnoreCase)>=0||w.Sciezka.IndexOf(filtr,StringComparison.OrdinalIgnoreCase)>=0).ToList();
        UkladFolderu uklad; if(!Kafelki.Ustawienia.UkladyFolderow.TryGetValue(sciezka,out uklad))uklad=new UkladFolderu();
        string pole=uklad.Sortowanie;bool malejaco=uklad.Malejaco,foldery=Kafelki.Ustawienia.FolderyNaPoczatku;UzupelnijDaneSortowania(wynik,pole);
        wynik.Sort((pierwszy,drugi)=>SortowanieElementow.Porownaj(pierwszy,drugi,pole,malejaco,foldery));
        return wynik.Skip(od).Take(limit).ToList();
    }
    void UzupelnijKopie(Baza baza,IEnumerable<Wiersz> dane){foreach(var w in dane.Where(p=>p.Rodzaj=="plik")){var grupa=baza.Zapytaj("SELECT g.id,g.lokalizacje FROM lokalizacje_duplikatow l JOIN grupy_duplikatow g ON g.id=l.grupa WHERE l.sciezka=? AND g.sha256=? AND g.rozmiar=? LIMIT 1",w.Sciezka,w.Hash,w.Rozmiar);if(grupa.Count>0){w.Grupa=Convert.ToInt64(grupa[0][0]);w.Kopie=Convert.ToInt64(grupa[0][1]);}else if(String.IsNullOrEmpty(w.Hash)&&w.Rozmiar>0)w.Kandydaci=Convert.ToInt64(baza.Wartosc("SELECT count(*) FROM pliki WHERE rozmiar=?",w.Rozmiar));}}
    async Task UzupelnijDaty(List<Wiersz> dane,int numerWersji){await Task.Run(()=>{foreach(var w in dane.Where(p=>p.Rodzaj=="plik"||p.Rodzaj=="folder")){try{FileSystemInfo plik=w.Rodzaj=="folder"?(FileSystemInfo)new DirectoryInfo(w.Sciezka):new FileInfo(w.Sciezka);if(plik.Exists&&(plik.Attributes&(FileAttributes.Offline|FileAttributes.ReparsePoint))==0&&(((int)plik.Attributes)&0x440000)==0)w.Utworzenie=plik.CreationTime;}catch(IOException){}catch(UnauthorizedAccessException){}}});if(numerWersji==wersja)lista.Items.Refresh();}
}
}
