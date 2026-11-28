import { Alert, Anchor, EmptyState, Group, Loader } from '@mantine/core';
import { useQueryClient } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { ArrowLeftRightIcon, CheckIcon, GiftIcon, InfoIcon, PackageOpenIcon, PlusIcon, RefreshCwIcon, TriangleAlertIcon, XIcon } from 'lucide-react';
import React from 'react';
import { CloudApprovalNotice } from '../cloud/cloud-approval-notice';
import { useCloudStatus } from '../data/sdk/cloud/cloud.gen';
import { CloudAccountStatus, TradeError, TradeKind, TradeStatus, type TradeActionResultDTO, type TradeDTO } from '../data/sdk/model';
import { useTradeAccept, useTradeCancel, useTradeDecline, useTradeStatus } from '../data/sdk/trade/trade.gen';
import { withErrorCatcher } from '../error/with-error-catcher';
import { TradePokemon } from '../trades/trade-pokemon';
import { TradePokemonPicker } from '../trades/trade-pokemon-picker';
import { tradeErrorKeys } from '../trades/trade-util';
import { useTranslate } from '../translate/i18n';
import { UIButton } from '../ui/form/button/ui-button';
import { UISegmentedControl } from '../ui/form/select/ui-segmented-control';
import { UIConfirmPopover } from '../ui/popover/ui-confirm-popover';
import { UISavesContent } from '../ui/saves/ui-saves-content';
import { UISavesHeader, UISavesScroll } from '../ui/saves/ui-saves-layout';
import { UITradeCard, UITradeStatus, type UITradeTone } from '../ui/trades/ui-trade-card';
import { renderDate, renderTime } from '../util/render-date-time';

type Tab = 'all' | 'received' | 'sent' | 'history';

const isActive = (trade: TradeDTO) => trade.status === TradeStatus.OPEN || trade.status === TradeStatus.COUNTERED;

/** Who must act now: the sender answers a counter-offer, the receiver answers an offer. */
const isWaitingForMe = (trade: TradeDTO) => trade.isSender
    ? trade.status === TradeStatus.COUNTERED
    : trade.status === TradeStatus.OPEN;

const getTone = (trade: TradeDTO): UITradeTone => {
    if (isActive(trade))
        return isWaitingForMe(trade) ? 'you' : 'friend';
    return trade.status === TradeStatus.DONE ? 'done' : 'failed';
};

const getStatusKey = (trade: TradeDTO) => {
    switch (trade.status) {
        case TradeStatus.OPEN:
        case TradeStatus.COUNTERED:
            return isWaitingForMe(trade) ? 'trades.status.waiting-you' : 'trades.status.waiting-friend';
        case TradeStatus.DONE:
            return 'trades.status.done';
        case TradeStatus.DECLINED:
            return trade.closedByMe === true ? 'trades.status.declined-by-you'
                : trade.closedByMe === false ? 'trades.status.declined-by-friend'
                    : 'trades.status.declined';
        case TradeStatus.CANCELLED:
            return trade.closedByMe === false || (trade.closedByMe == null && !trade.isSender)
                ? 'trades.status.cancelled-by-friend'
                : 'trades.status.cancelled-by-you';
        case TradeStatus.EXPIRED:
            return 'trades.status.expired';
    }
};

const emptyKeys = {
    all: 'trades.empty.all',
    received: 'trades.empty.received',
    sent: 'trades.empty.sent',
    history: 'trades.empty.history',
} as const satisfies Record<Tab, string>;

const renderDateTime = (value: string) => {
    const date = new Date(value);
    return `${renderDate(date)} ${renderTime(date).slice(0, 5)}`;
};

export const TradesPage: React.FC = withErrorCatcher('default', () => {
    const { t } = useTranslate();
    const queryClient = useQueryClient();

    const [ tab, setTab ] = React.useState<Tab>('all');
    const [ lastResult, setLastResult ] = React.useState<TradeActionResultDTO>();
    const [ answering, setAnswering ] = React.useState<TradeDTO>();

    // kept up to date by TradesWatcher
    const statusQuery = useTradeStatus();
    const status = statusQuery.data?.data;
    const accountStatus = useCloudStatus().data?.data.accountStatus;
    const notApproved = status?.error === TradeError.NOT_APPROVED;

    const acceptMutation = useTradeAccept();
    const declineMutation = useTradeDecline();
    const cancelMutation = useTradeCancel();

    const run = async (action: () => Promise<{ data: TradeActionResultDTO }>) => {
        const response = await action();
        setLastResult(response.data);
        // pokémon may have left or entered the storage
        await queryClient.invalidateQueries();
    };

    const trades = status?.trades ?? [];
    const received = trades.filter(trade => !trade.isSender && isActive(trade));
    const sent = trades.filter(trade => trade.isSender && isActive(trade));
    const history = trades.filter(trade => !isActive(trade));
    const shown = { all: trades, received, sent, history }[ tab ];

    const renderActions = (trade: TradeDTO) => {
        if (!isActive(trade))
            return null;

        const decline = <UIConfirmPopover
            key='decline'
            label={t('trades.action.decline')}
            description={t('trades.action.decline.description')}
            color='red'
            action={() => run(() => declineMutation.mutateAsync({ tradeId: trade.id }))}
        >
            <UIButton name={`trade-decline-${trade.id}`} controlLabel={t('trades.action.decline')}
                variant='default' color='red' size='compact-md' leftSection={<XIcon />}>
                {t('trades.action.decline')}
            </UIButton>
        </UIConfirmPopover>;

        if (!trade.isSender && trade.status === TradeStatus.OPEN && trade.kind === TradeKind.TRADE) {
            return decline;
        }

        if (!trade.isSender && trade.status === TradeStatus.OPEN) {
            return <>
                {decline}
                <UIConfirmPopover
                    label={t('trades.action.accept-gift')}
                    description={t('trades.action.accept-gift.description')}
                    action={() => run(() => acceptMutation.mutateAsync({ tradeId: trade.id }))}
                >
                    <UIButton name={`trade-accept-${trade.id}`} controlLabel={t('trades.action.accept-gift')}
                        variant='filled' size='compact-md' leftSection={<GiftIcon />}>
                        {t('trades.action.accept-gift')}
                    </UIButton>
                </UIConfirmPopover>
            </>;
        }

        if (!trade.isSender && trade.status === TradeStatus.COUNTERED) {
            return decline;
        }

        if (trade.isSender && trade.status === TradeStatus.COUNTERED) {
            return <>
                {decline}
                <UIConfirmPopover
                    label={t('trades.action.accept')}
                    description={t('trades.action.accept.description')}
                    action={() => run(() => acceptMutation.mutateAsync({ tradeId: trade.id }))}
                >
                    <UIButton name={`trade-accept-${trade.id}`} controlLabel={t('trades.action.accept')}
                        variant='filled' size='compact-md' leftSection={<CheckIcon />}>
                        {t('trades.action.accept')}
                    </UIButton>
                </UIConfirmPopover>
            </>;
        }

        return <UIConfirmPopover
            label={t('trades.action.cancel')}
            description={t('trades.action.cancel.description')}
            color='red'
            action={() => run(() => cancelMutation.mutateAsync({ tradeId: trade.id }))}
        >
            <UIButton name={`trade-cancel-${trade.id}`} controlLabel={t('trades.action.cancel')}
                variant='default' color='red' size='compact-md' leftSection={<XIcon />}>
                {t('trades.action.cancel')}
            </UIButton>
        </UIConfirmPopover>;
    };

    const renderHint = (trade: TradeDTO) => {
        if (!isActive(trade))
            return null;
        if (!trade.isSender && trade.status === TradeStatus.OPEN && trade.kind === TradeKind.TRADE)
            return t('trades.hint.counter');
        if (!trade.isSender && trade.status === TradeStatus.OPEN)
            return t('trades.hint.gift', { name: trade.fromName });
        if (!trade.isSender && trade.status === TradeStatus.COUNTERED)
            return t('trades.hint.waiting-sender', { name: trade.fromName });
        if (trade.isSender && trade.status === TradeStatus.OPEN)
            return t('trades.hint.waiting-friend', { name: trade.toName });
        return null;
    };

    return <UISavesContent>
        <UISavesScroll>
            <UISavesHeader
                title={t('trades.title')}
                description={t('trades.subtitle')}
                actions={<UIButton
                    name='trades-refresh'
                    controlLabel={t('trades.refresh')}
                    onClick={() => statusQuery.refetch()}
                    loading={statusQuery.isFetching}
                    leftSection={<RefreshCwIcon />}
                    variant='default'
                >
                    {t('trades.refresh')}
                </UIButton>}
            />

            {status && !status.loggedIn && <Alert variant='outline' icon={<InfoIcon />}>
                {t('trades.logged-out')}{' '}
                <Anchor renderRoot={props => <Link {...props} to='/settings' search={{ subMenu: 'cloud' }} />}>{t('trades.logged-out.link')}</Anchor>
            </Alert>}

            {notApproved && <CloudApprovalNotice accountStatus={accountStatus ?? CloudAccountStatus.PENDING} />}

            {status?.error !== undefined && status.loggedIn && !notApproved && <Alert variant='outline' color='yellow' icon={<TriangleAlertIcon />}>
                {t(tradeErrorKeys[ status.error ])} {status.errorDetail}
            </Alert>}

            {lastResult && !lastResult.success && lastResult.error !== undefined && <Alert
                variant='outline' color='yellow' icon={<TriangleAlertIcon />}
                withCloseButton onClose={() => setLastResult(undefined)}
            >
                {t(tradeErrorKeys[ lastResult.error ])} {lastResult.errorDetail}
            </Alert>}

            {!!status?.toClaimCount && <Alert variant='outline' icon={<PackageOpenIcon />}>
                {t('trades.to-claim', { count: status.toClaimCount })}
            </Alert>}

            {status?.loggedIn && !notApproved && <>
                <UISegmentedControl<Tab>
                    name='trades-tab'
                    controlLabel={t('trades.title')}
                    value={tab}
                    onChange={setTab}
                    data={[
                        { value: 'all', label: `${t('trades.tab.all')} (${trades.length})` },
                        { value: 'received', label: `${t('trades.tab.received')} (${received.length})` },
                        { value: 'sent', label: `${t('trades.tab.sent')} (${sent.length})` },
                        { value: 'history', label: t('trades.tab.history') },
                    ]}
                    style={{ alignSelf: 'flex-start' }}
                />

                {tab === 'sent' && <Group gap='xs' c='dimmed' fz='sm'>
                    <ArrowLeftRightIcon size={16} />
                    {t('trades.hint.offer')}
                </Group>}

                {shown.length === 0 && <EmptyState
                    size='sm'
                    py='xl'
                    icon={<ArrowLeftRightIcon />}
                    title={t(emptyKeys[ tab ])}
                />}

                {shown.map(trade => <UITradeCard
                    key={trade.id}
                    people={trade.isSender
                        ? t('trades.card.to', { name: trade.toName })
                        : t('trades.card.from', { name: trade.fromName })}
                    tone={getTone(trade)}
                    status={<UITradeStatus tone={getTone(trade)}>
                        {t(getStatusKey(trade), { name: trade.isSender ? trade.toName : trade.fromName })}
                    </UITradeStatus>}
                    date={isActive(trade)
                        ? t('trades.card.expires', { date: renderDateTime(trade.expiresAt) })
                        : renderDateTime(trade.closedAt ?? trade.created)}
                    offered={<TradePokemon pokemon={trade.offered} emptyLabel='' />}
                    countered={<TradePokemon
                        pokemon={trade.countered}
                        emptyLabel={trade.kind === TradeKind.GIFT
                            ? t('trades.card.gift')
                            : trade.isSender && trade.status === TradeStatus.OPEN
                                ? t('trades.card.waiting-choice', { name: trade.toName })
                                : t('trades.card.nothing-back')}
                        emptyAction={!trade.isSender && trade.status === TradeStatus.OPEN && trade.kind === TradeKind.TRADE
                            ? <UIButton
                                name={`trade-answer-${trade.id}`}
                                controlLabel={t('trades.action.choose')}
                                variant='default'
                                size='compact-md'
                                leftSection={<PlusIcon />}
                                onClick={() => setAnswering(trade)}
                            >
                                {t('trades.action.choose')}
                            </UIButton>
                            : undefined}
                    />}
                    messages={[
                        trade.message && `${trade.fromName}: ${trade.message}`,
                        trade.counterMessage && `${trade.toName}: ${trade.counterMessage}`,
                        renderHint(trade),
                    ]}
                    actions={renderActions(trade)}
                />)}
            </>}

            {statusQuery.isPending && <Group justify='center' py='xl'><Loader size='sm' /></Group>}

            {answering && <TradePokemonPicker
                trade={answering}
                opened
                onClose={() => setAnswering(undefined)}
            />}
        </UISavesScroll>
    </UISavesContent>;
});
