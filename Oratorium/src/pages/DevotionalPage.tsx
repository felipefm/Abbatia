import { useParams } from 'react-router-dom'
import { useDevotional } from '../hooks/useDevotional'
import { LiturgicalHeader } from '../components/LiturgicalHeader'
import { DateNav } from '../components/DateNav'
import { SaintCard } from '../components/SaintCard'
import { ReadingsList } from '../components/ReadingsList'
import { HomilyCard } from '../components/HomilyCard'
import { LoadingState, ErrorState } from '../components/StatusStates'
import { TableOfContents } from '../components/TableOfContents'
import { MonthCalendar } from '../components/MonthCalendar'
import { ColorMeaningCard } from '../components/sidebar/ColorMeaningCard'
import { PullQuoteCard } from '../components/sidebar/PullQuoteCard'
import { HomilySourceCard } from '../components/sidebar/HomilySourceCard'
import { RosaryMysteryCard } from '../components/sidebar/RosaryMysteryCard'
import { SeasonCountdownCard } from '../components/sidebar/SeasonCountdownCard'
import { OtherSaintsCard } from '../components/sidebar/OtherSaintsCard'
import { DiaryCard } from '../components/sidebar/DiaryCard'
import { isValidIsoDate, todayISO } from '../lib/date'

/** Página do devocional de um dia — usada tanto por "/hoje" (`date` de
 * rota indefinido, pede a data atual à API) quanto por "/dia/:date".
 *
 * LAYOUT DE 3 COLUNAS (mobile-first): abaixo de `lg:` é uma coluna só,
 * exatamente como antes (o `order-*` garante que o conteúdo principal vem
 * primeiro na tela, sidebars depois) — a partir de `lg:` vira grid de 3
 * colunas com as duas sidebars fixas (`sticky`) ao rolar. */
export function DevotionalPage() {
  const { date } = useParams<{ date?: string }>()
  const { data, loading, slow, error, retry } = useDevotional(date)

  const displayDate = data?.date ?? (date && isValidIsoDate(date) ? date : todayISO())

  return (
    <div className="mx-auto max-w-6xl px-4 py-6 lg:grid lg:grid-cols-[240px_minmax(0,1fr)_300px] lg:items-start lg:gap-6">
      <aside className="order-2 flex flex-col gap-4 lg:sticky lg:top-6 lg:order-1 lg:self-start">
        <TableOfContents hasHomily={!!data?.homily} />
        {data && <MonthCalendar currentDate={displayDate} />}
      </aside>

      <main className="order-1 flex flex-col gap-4 lg:order-2">
        <DateNav currentDate={displayDate} />

        {loading && <LoadingState slow={slow} />}
        {error && !loading && <ErrorState error={error} onRetry={retry} />}

        {data && !loading && !error && (
          <>
            <LiturgicalHeader
              date={data.date}
              liturgicalTitle={data.liturgicalTitle}
              liturgicalColor={data.liturgicalColor}
            />
            {data.saint && <SaintCard saint={data.saint} />}
            <ReadingsList readings={data.readings} />
            {data.homily && <HomilyCard homily={data.homily} />}
          </>
        )}
      </main>

      {data && !loading && !error && (
        <aside className="order-3 flex flex-col gap-4 lg:sticky lg:top-6 lg:self-start">
          <ColorMeaningCard color={data.liturgicalColor} />
          <PullQuoteCard readings={data.readings} homily={data.homily} />
          {data.homily && <HomilySourceCard homily={data.homily} />}
          <RosaryMysteryCard date={displayDate} />
          <SeasonCountdownCard date={displayDate} />
          <OtherSaintsCard saints={data.otherSaints} />
          <DiaryCard date={displayDate} />
        </aside>
      )}
    </div>
  )
}
