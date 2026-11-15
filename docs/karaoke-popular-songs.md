# Popular karaoke songs (list)

`karaoke-popular-songs.csv`: 255 songs most sung at karaoke, compiled on 2026-11-15 from
three charts based on what people actually sing in karaoke apps (not on streaming or
editorial picks). It opens in Excel/LibreOffice (UTF-8, comma separated).

| Source | What it counts | Songs used |
|---|---|---|
| [KaraFun – Top Italia](https://www.karafun.it/karaoke/playlist/top-italia-29/) | most sung by KaraFun users in Italy (Italian and foreign songs) | 100 |
| [Singa – best Italian karaoke songs](https://singa.com/blog/best-italian-karaoke-songs/) | songs sung in Italian in the Singa app, last 12 months (updated May 2026) | 100 (97 after merging its duplicates) |
| [KaraFun – Top mondiali](https://www.karafun.it/karaoke/playlist/top-mondiali-100/) | most sung worldwide | first 96 of 199 |

Columns:

- `rank`, `score`: each list gives a song 1 point at the top down to about 0 at the bottom;
  the points are added, so songs in two or three lists come first.
- `artist`, `title`: cleaned up (the sources show film names or truncated artists).
- `language`: `it`, `en`, `es`, `it/en`.
- `category`: `lirica` for the opera arias of the Singa list (popular with foreign users of
  the app, less so at an Italian karaoke night: easy to filter out).
- `sources`: the list(s) and the position in each.

Not included: Singa's United States chart (mostly American country songs) and editorial
"best karaoke songs" articles. The charts change over time: the list is a snapshot.
