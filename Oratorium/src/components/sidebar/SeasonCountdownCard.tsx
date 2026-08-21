import { Card } from '../Card'
import { getNextSeasonCountdown } from '../../lib/liturgicalSeasons'

export function SeasonCountdownCard({ date }: { date: string }) {
  const countdown = getNextSeasonCountdown(date)

  return (
    <Card eyebrow="Próximo tempo litúrgico">
      <p className="mt-1 font-serif-reading text-lg text-neutral-900 dark:text-neutral-50">{countdown.seasonName}</p>
      <p className="mt-1 text-sm text-neutral-600 dark:text-neutral-400">
        {countdown.daysUntil === 0
          ? 'Começa hoje.'
          : countdown.daysUntil === 1
            ? 'Começa amanhã.'
            : `Faltam ${countdown.daysUntil} dias.`}
      </p>
    </Card>
  )
}
