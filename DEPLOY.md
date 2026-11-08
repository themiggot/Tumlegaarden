# Udgivelse til Simply.com

Komplet vejledning til at lægge Tumlegaarden-sitet på webhotellet hos Simply.com og holde det opdateret.
Følg trinene i rækkefølge første gang. Afsnit 8 beskriver opdateringer bagefter.

## Sådan hænger det sammen

```
GitHub: main  ──(GitHub Actions bygger)──►  GitHub: release  ──(Simply "Importer fra Git")──►  /public_html
   kildekode                                 færdigbygget site                                    det kørende site

Simply.com:
/
├── public_html/          ← sitet. Erstattes ved hver import. Gem aldrig noget her selv.
├── tumlegaarden-data/    ← hemmeligheder og data. Røres aldrig af importen.
│   ├── appsettings.Production.json   (du opretter den, trin 3)
│   └── keys/                         (oprettes af sitet: nøgler til login-cookies)
└── tumlegaarden-shadow/  ← oprettes af IIS: kopien af appen, der rent faktisk kører
```

- Grenen **`main`** indeholder kildekoden. Her laves ændringer.
- Grenen **`release`** bygges automatisk af GitHub ved hver ændring på `main`. Den er repoets **standard-gren**, fordi Simply's import kun henter standard-grenen.
- Data og adgangskoder ligger i **`/tumlegaarden-data`**, uden for web-mappen, så en import aldrig kan slette eller udstille dem.

**Du skal bruge:** adgang til Simply.com's kontrolpanel for `tumlegaarden.dk` og til GitHub-repoet `themiggot/tumlegaarden`.

---

## 1. Tjek at webhotellet kan køre ASP.NET

Sitet er en ASP.NET Core 10-app og kræver Simply's Windows/IIS-hosting med ASP.NET. Et rent PHP/Linux-webhotel kan ikke køre det.

1. Se under **Website → Oversigt** og **Administration**, om der står ASP.NET, .NET eller Windows.
2. Er du i tvivl, så skriv til Simply's support:
   > Understøtter webhotellet for tumlegaarden.dk ASP.NET Core 10 som self-contained app, skal vi udgive til win-x64 eller win-x86, og må appen læse og skrive i mapper ved siden af public_html?
3. **Siger Simply x86:** på GitHub, gå til **Settings → Secrets and variables → Actions → Variables → New repository variable**. Navn `RUNTIME`, værdi `win-x86`. Gå så til **Actions → Byg og udgiv → Run workflow**.

---

## 2. Forbered Simply.com

### 2a. HTTPS
**Website → HTTPS beskyttelse**: slå SSL-certifikat til for `tumlegaarden.dk` og `www.tumlegaarden.dk`. Sitet kræver HTTPS.

### 2b. MySQL-database
**Databaser → MySQL**: opret en database. Notér server (værtsnavn), databasenavn, brugernavn og adgangskode.
Tabellerne oprettes automatisk af sitet ved første start.

### 2c. Mailkonto til afsendelse
**E-mail**: opret `noreply@tumlegaarden.dk` med en lang, tilfældig adgangskode.
Find SMTP-serveren under mailkontoens opsætning. Sitet er sat op til `smtp.simply.com` på port 587. Står der noget andet, så brug det.

---

## 3. Opret datamappen med hemmelighederne

Hemmelighederne må **aldrig** ligge på GitHub eller i `public_html`.

1. **Website → File Manager**. Klik **Gå et niveau op**, så der står **Mappe: /** og du kan se `public_html`.
2. **Ny → Mappe**: opret `tumlegaarden-data`. Den skal ligge ved siden af `public_html`, ikke inde i den.
3. Gå ind i `tumlegaarden-data` og opret filen `appsettings.Production.json` (**Ny → Fil**, eller lav den på din computer og brug **Upload**). Indhold:

```json
{
  "Bootstrap": {
    "AdminEmail": "DIT_NAVN@tumlegaarden.dk"
  },
  "ConnectionStrings": {
    "MySql": "Server=MYSQL_SERVER;Port=3306;Database=DATABASENAVN;User Id=BRUGERNAVN;Password=ADGANGSKODE;SslMode=Required;CharSet=utf8mb4"
  },
  "Smtp": {
    "Host": "smtp.simply.com",
    "Port": 587,
    "UserName": "noreply@tumlegaarden.dk",
    "Password": "MAILKONTOENS_ADGANGSKODE",
    "FromAddress": "noreply@tumlegaarden.dk"
  }
}
```

| Felt | Hvad det er |
|---|---|
| `Bootstrap:AdminEmail` | Den første administrator. Får en invitation, når databasen er tom. Skal være en `@tumlegaarden.dk`-adresse, du kan læse. |
| `ConnectionStrings:MySql` | Oplysningerne fra trin 2b. Indeholder adgangskoden et `;`, så kontakt udvikleren. |
| `Smtp:*` | Mailkontoen fra trin 2c. Port 587 bruger STARTTLS, port 465 bruger SSL. |

Laver du filen på din computer, så brug en teksteditor (Notesblok, VS Code), ikke Word, og gem som UTF-8.

---

## 4. Tjek repoet på GitHub (én gang)

*Importer fra Git* kræver:
- **Repoet er offentligt.** Det er det nu. Det indeholder ingen adgangskoder, men alle kan læse koden.
- **Standard-grenen er `release`.** Tjek under **Settings → General → Default branch**, at der står `release`.
- **Seneste kørsel under Actions → Byg og udgiv er grøn.**

---

## 5. Udgiv med "Importer fra Git"

1. **Website → File Manager**, gå til `/public_html`.
2. **Slet** `index.html` (Simply's standardside), hvis den findes. **Lad** mappen `.well-known` **være**.
3. **Upload ▾ → Importer fra Git**, og udfyld:
   - **Git-URL:** `https://github.com/themiggot/tumlegaarden.git`
   - **Destinations-mappe:** `/public_html`
   - Sæt flueben i **Jeg er indforstået med virkningen af denne funktion**.
4. Klik **Importer** og vent, til den er færdig. Pakken er omkring 135 MB.
5. Tjek at `/public_html` nu indeholder `web.config`, `Tumlegaarden.Web.exe`, mappen `wwwroot` og `version.txt`.
   `version.txt` viser, hvilken commit der er udgivet.

---

## 6. Første start

1. Åbn **https://tumlegaarden.dk**. Første besøg kan tage 10–20 sekunder, mens appen starter og opretter tabellerne.
2. Du skal se login-siden med overskriften **TUMLEGAARDEN** og cookie-meddelelsen nederst.
3. Tjek at `/tumlegaarden-data` nu også indeholder mappen `keys`. Det viser, at sitet har fundet datamappen.
4. Tjek indbakken for `Bootstrap:AdminEmail`. Der ligger en mail: *"Tumlegaarden: Du er inviteret som administrator"*. Tjek også spam.
5. Åbn linket, vælg adgangskode (mindst 14 tegn, store og små bogstaver, tal og specialtegn).
6. Scan QR-koden med en authenticator-app og indtast koden.
7. **Gem de 10 gendannelseskoder** et sikkert sted, fx i en adgangskodemanager.
8. Gå til **Brugere** og invitér resten af holdet.

Ser du noget andet end login-siden, så gå til afsnit 9.

---

## 7. Kontroller at alt er i orden

- [ ] `http://tumlegaarden.dk` sender videre til `https://`.
- [ ] Login-siden vises, og cookie-meddelelsen forsvinder efter **OK**.
- [ ] Mappen `/tumlegaarden-data/keys` findes.
- [ ] Administrator-invitationen kom frem, og to-faktor virker.
- [ ] En invitation til en kollega kommer frem.
- [ ] En `@gmail.com`-adresse afvises ved invitation.
- [ ] [securityheaders.com](https://securityheaders.com/?q=tumlegaarden.dk) giver karakter A eller A+.
- [ ] `https://tumlegaarden.dk/version.txt` og `https://tumlegaarden.dk/web.config` sender dig til login og viser **ikke** filen.

---

## 8. Opdatering af sitet

1. Ændringer laves på `main` på GitHub.
2. Vent til **Actions → Byg og udgiv** er grøn (2–3 minutter). Så er `release` opdateret.
3. Kør **Importer fra Git** igen som i trin 5.
4. Sitet genstarter selv med den nye version ved næste besøg. Tjek `version.txt` i File Manager.

**Hvis importen fejler med låste filer:** server-softwaren understøtter ikke shadow copy. Gør så sådan:
1. Upload `app_offline.htm` fra repoets mappe `deploy/` (på `main`) til `/public_html`. Appen stopper og viser en "opdaterer"-side.
2. Kør **Importer fra Git**.
3. Slet `app_offline.htm`, hvis den stadig ligger der.

Rør aldrig `/tumlegaarden-data`. Slettes `keys`, bliver alle logget ud. Slettes `appsettings.Production.json`, kan sitet ikke nå databasen.

---

## 9. Fejlsøgning

Slå detaljeret log til, når noget fejler:
1. Opret mappen `logs` i `/public_html`.
2. Ret `stdoutLogEnabled="false"` til `"true"` i `/public_html/web.config`.
3. Genindlæs siden og læs den nyeste fil i `logs/`. Se også **Website → Webserver logs**.
4. Rettelsen forsvinder ved næste import, og det er meningen.

Første linjer i loggen viser `Datamappe: …`. Det er mappen, sitet læser hemmeligheder fra.

| Symptom | Sandsynlig årsag | Løsning |
|---|---|---|
| Du ser indholdet af `web.config`, en mappeoversigt, 403 eller PHP-fejl | Webhotellet kører ikke ASP.NET | Se trin 1. Kontakt Simply. |
| Importen fejler: "repository not found" eller lignende | Repoet er privat, eller URL'en er forkert | Se trin 4. URL'en skal ende på `.git`. |
| Importen henter kildekode (`src/`, `README.md`) i stedet for `web.config` | Standard-grenen er `main`, ikke `release` | Sæt **Default branch** til `release` (trin 4) og importér igen. |
| **HTTP 500.19** | IIS forstår ikke en linje i `web.config` | Prøv at fjerne `removeServerHeader="true"`. Rettelsen skal laves i `src/Tumlegaarden.Web/web.config` på `main`. Kontakt udvikleren. |
| **HTTP 500.30 / 500.32** | Appen kan ikke starte, ofte x64/x86 | Sæt `RUNTIME` (trin 1), vent på Actions, importér igen. Ellers læs loggen. |
| 500-fejl, der nævner shadow copy, eller `tumlegaarden-shadow` kan ikke oprettes | IIS må ikke skrive ved siden af `public_html` | Opret mappen `/tumlegaarden-shadow` selv i File Manager. Hjælper det ikke, så bed udvikleren slå shadow copy fra og brug `app_offline.htm` ved opdateringer (afsnit 8). |
| Loggen siger "Datamappen ligger inde i web-mappen" | `/tumlegaarden-data` findes ikke eller ligger forkert | Tjek at mappen ligger ved siden af `public_html` og er stavet præcis sådan (trin 3). |
| **HTTP 500.30** med databasefejl i loggen | Forkert forbindelsesstreng | Tjek server, bruger og adgangskode. Ved SSL-fejl: skift `SslMode=Required` til `SslMode=Preferred`. |
| **HTTP 400 Bad Request – Invalid Hostname** | Sitet tilgås via en adresse, der ikke står i `AllowedHosts` | Tilføj adressen i `AllowedHosts` i `appsettings.Production.json`, fx `"AllowedHosts": "tumlegaarden.dk;www.tumlegaarden.dk;ny.adresse.dk"`. |
| Ingen invitationsmail | SMTP-oplysninger forkerte, eller mailen er i spam | Læs loggen ("Kunne ikke sende"). Tjek trin 2c og 3. |
| Kom aldrig nogen administrator-invitation | `Bootstrap:AdminEmail` mangler, eller der findes allerede en bruger | Sendes kun når databasen er tom. Ret filen og genstart ved at uploade `app_offline.htm` og slette den igen. |
| Alle bliver logget ud efter hver opdatering | `keys` ligger i `public_html` i stedet for `/tumlegaarden-data` | Se rækken om datamappen ovenfor. |
| "For mange forsøg" | Rate limiting: maks. 30 loginforsøg pr. minut pr. IP | Vent et minut. |
| Brugeren har mistet telefon og gendannelseskoder | | En administrator vælger **Nulstil 2FA** under Brugere. |

---

## 10. Alternativ: upload med FTP

Brug denne metode, hvis *Importer fra Git* ikke virker, eller repoet skal være privat.

1. Hent pakken: GitHub → **Actions → Byg og udgiv** → seneste grønne kørsel → **Artifacts** → `tumlegaarden-win-x64`. Pak zip-filen ud.
2. Forbind med [FileZilla](https://filezilla-project.org/) eller [WinSCP](https://winscp.net/) med oplysningerne fra **File Manager → Loginoplysninger**. Brug SFTP eller **Kræv eksplicit FTP over TLS**, aldrig ukrypteret FTP.
3. Ved opdatering: upload `deploy/app_offline.htm` til `/public_html` først.
4. Upload **indholdet** af den udpakkede mappe til `/public_html` og overskriv.
5. Slet `app_offline.htm`.

Trin 2–3 (Simply-forberedelse og datamappe) gælder også her.

---

## 11. Backup og sikkerhed

- **Database:** Tag jævnligt backup af MySQL-databasen via kontrolpanelet. Se også **Website → Genskab data**.
- **`/tumlegaarden-data`:** Tag backup af hele mappen sammen med databasen.
- **Hemmeligheder:** `appsettings.Production.json` findes kun i `/tumlegaarden-data` og hos dig. Læg den aldrig på GitHub eller i mails. Skift adgangskoderne, hvis den slipper ud.
- **Repoet er offentligt.** Læg aldrig adgangskoder, forbindelsesstrenge eller persondata i repoet. GitHub Actions tjekker, at `appsettings.Production.json` ikke kommer med i pakken.
- **Administratorer:** Hav altid mindst to administratorer, så en mistet telefon ikke låser alle ude.
