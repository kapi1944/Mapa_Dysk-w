import sqlite3,json,shutil,hashlib,sys,os,time
from pathlib import Path

katalog=Path(__file__).resolve().parents[1]
audyt=Path(sys.argv[1]).resolve()
indeks=katalog/'dane'/'indeks.sqlite'
if indeks.exists(): raise SystemExit('Indeks już istnieje. Przygotowanie nie nadpisuje działającej bazy.')
shutil.copyfile(audyt/'mapa-danych.sqlite',indeks)
baza=sqlite3.connect(indeks)
baza.execute('CREATE TABLE widok_folderow(sciezka TEXT PRIMARY KEY,rodzic TEXT,nazwa TEXT,rozmiar INTEGER,pliki INTEGER,podfoldery INTEGER,zmiana REAL,filmy INTEGER,zdjecia INTEGER,dokumenty INTEGER,archiwa INTEGER,inne INTEGER,duplikaty INTEGER,sygnal INTEGER,systemowy INTEGER,uwagi TEXT,ostatni_skan REAL)')
baza.execute('CREATE TABLE grupy_duplikatow(id INTEGER PRIMARY KEY,sha256 TEXT,rozmiar INTEGER,lokalizacje INTEGER,niezalezne INTEGER,zajete INTEGER,potencjal INTEGER,niepewne INTEGER)')
baza.execute('CREATE TABLE lokalizacje_duplikatow(grupa INTEGER,sciezka TEXT,folder TEXT)')
baza.execute('CREATE TABLE dyski(sciezka TEXT PRIMARY KEY,pojemnosc INTEGER,zajete INTEGER,wolne INTEGER)')
foldery=json.loads((audyt/'foldery.json').read_text(encoding='utf-8'))
podsumowanie=json.loads((audyt/'podsumowanie.json').read_text(encoding='utf-8'))
wiersze=[]
for folder in foldery:
    sciezka=folder['sciezka']
    wiersze.append((sciezka,folder['rodzic'],os.path.basename(sciezka.rstrip('\\')) or sciezka,folder['rozmiar'],folder['pliki'],folder['podfoldery'],folder['zmiana'],*[folder['typy'][typ]['bajty'] for typ in ['filmy','zdjecia','dokumenty','archiwa','inne']],folder['duplikaty_bajty'],int(folder['sygnal_kopii']),0,'',0))
baza.executemany('INSERT INTO widok_folderow VALUES('+','.join('?'*17)+')',wiersze)
zdarzenia=json.loads((audyt/'zakres-i-bledy.json').read_text(encoding='utf-8'))
for zdarzenie in zdarzenia:
    sciezka=zdarzenie['sciezka']
    if zdarzenie['rodzaj']=='niskopriorytetowy_pominiety' and 'biezacego audytu' in zdarzenie['opis']:
        if sciezka.casefold()==str(audyt.parent).casefold(): continue
        rodzic=os.path.dirname(sciezka)
        baza.execute('INSERT OR IGNORE INTO widok_folderow VALUES(?,?,?,-1,-1,-1,0,0,0,0,0,0,0,0,1,?,0)',(sciezka,rodzic,os.path.basename(sciezka),'SYSTEMOWY — NIE RUSZAĆ • Zakres nieprzeskanowany'))
    if zdarzenie['rodzaj']=='blad': baza.execute('UPDATE widok_folderow SET uwagi=? WHERE sciezka=?',('NIEPEŁNY ODCZYT: '+zdarzenie['opis'],sciezka))
duplikaty=json.loads((audyt/'duplikaty.json').read_text(encoding='utf-8'))
for ident,grupa in enumerate(duplikaty,1):
    baza.execute('INSERT INTO grupy_duplikatow VALUES(?,?,?,?,?,?,?,?)',(ident,grupa['sha256'],grupa['rozmiar'],len(grupa['pliki']),grupa['niezalezne_pliki'],grupa['niezalezne_pliki']*grupa['rozmiar'],grupa['potencjal_bajty_logiczne'],grupa.get('tozsamosci_niepotwierdzone',0)))
    baza.executemany('INSERT INTO lokalizacje_duplikatow VALUES(?,?,?)',[(ident,s,os.path.dirname(s)) for s in grupa['pliki']])
baza.executemany('INSERT INTO dyski VALUES(?,?,?,?)',[(d['sciezka'],d['pojemnosc'],d['zajete'],d['wolne']) for d in podsumowanie['dyski']])
for sql in ['CREATE INDEX foldery_rodzic ON widok_folderow(rodzic,systemowy,rozmiar DESC)','CREATE INDEX foldery_rozmiar ON widok_folderow(rozmiar DESC)','CREATE INDEX foldery_filmy ON widok_folderow(filmy DESC)','CREATE INDEX foldery_zdjecia ON widok_folderow(zdjecia DESC)','CREATE INDEX foldery_sygnal ON widok_folderow(sygnal,rozmiar DESC)','CREATE INDEX duplikaty_potencjal ON grupy_duplikatow(potencjal DESC)','CREATE INDEX lokalizacje_grupa ON lokalizacje_duplikatow(grupa)','CREATE INDEX lokalizacje_sciezka ON lokalizacje_duplikatow(sciezka)','CREATE INDEX foldery_zmiana ON widok_folderow(zmiana DESC)']:
    baza.execute(sql)
baza.commit();baza.close()
skrot=hashlib.sha256()
with (audyt/'mapa-danych.sqlite').open('rb') as plik:
    while blok:=plik.read(8*1024*1024):skrot.update(blok)
pochodzenie={'wersja':1,'audyt_utc':podsumowanie['czas_konca_utc'],'sha256_oryginalnego_indeksu':skrot.hexdigest(),'oryginalny_indeks':str(audyt/'mapa-danych.sqlite'),'katalog_wynikow_audytu':str(audyt.parent),'liczba_plikow':podsumowanie['liczba_plikow'],'liczba_folderow':podsumowanie['liczba_folderow'],'potencjal_bajty':podsumowanie['potencjal_bajty_logiczne']}
(katalog/'dane'/'pochodzenie.json').write_text(json.dumps(pochodzenie,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'indeks':str(indeks),'foldery':len(foldery),'duplikaty':len(duplikaty)},ensure_ascii=True))
