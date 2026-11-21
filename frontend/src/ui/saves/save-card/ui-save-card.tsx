import { Badge, Button, Group, Image } from '@mantine/core';
import { ArrowRightIcon, CopyIcon, PenIcon, TimerIcon } from 'lucide-react';
import type React from 'react';
import type { Gender } from '../../../data/sdk/model';
import { useTranslate } from '../../../translate/i18n';
import { useClickLoading } from '../../form/button/hooks/use-click-loading';
import { UIGender } from '../../icon/ui-gender';
import { WithControlsIcons } from '../../interaction/controls/icons/with-controls-icons';
import { getSelectControl } from '../../interaction/focus-controls/common-controls/select-controls';
import { useFocusControls } from '../../interaction/focus-controls/use-focus-controls';
import { UIPathLine } from '../../path/ui-path-line';
import { UIPokedexIcons } from '../../pokedex/icons/ui-pokedex-icons';
import { UIPopover } from '../../popover/ui-popover';
import classes from './ui-save-card.module.css';

export type UISaveCardProps = {
    id?: string;
    generation: string;
    label: React.ReactNode;
    imgSrc: string;
    path: string;
    ot?: string;
    otGender?: Gender;
    tid?: number;
    language?: string;
    caughtCount?: number;
    ownedCount?: number;
    shinyCount?: number;
    playTime?: string;
    loading?: boolean;
    disabled?: boolean;
    hasDuplicates?: boolean;
    onSelect?: () => unknown;
    editDropdown?: React.ReactNode;
    /** Small icon buttons shown top-right (download, delete, open folder). */
    actions?: React.ReactNode;
};

export const UISaveCard: React.FC<UISaveCardProps> = ({
    id, generation, label, imgSrc, path, ot, otGender, tid, language,
    caughtCount, ownedCount, shinyCount, playTime,
    loading: loadingInner, disabled, hasDuplicates, onSelect: onSelectInner, editDropdown, actions,
}) => {
    const { t } = useTranslate();

    const { onClick: onSelect, loading } = useClickLoading(onSelectInner, loadingInner);

    const { focusProps, controlProps, controlIcons } = useFocusControls({
        scopeNodeId: `game_${path}`,
        controls: [
            !disabled && onSelect && getSelectControl({
                label: t('action.select'),
                action: () => {
                    return onSelect();
                },
            }),
            !!editDropdown && {
                name: 'edit' as const,
                label: t('storage.actions.edit'),
                triggers: {
                    mouse: {
                        type: 'mouse',
                        values: [ 'left-click' ],
                    },
                    gamepad: {
                        type: 'gamepad',
                        values: [ 'X' ],
                    },
                },
                spread: false,
            },
        ],
    });

    const title = [ id, path ].filter(Boolean).join('\n');

    return <article className={classes.uiSaveCard}>
        <div className={classes.top}>
            <Image
                src={imgSrc}
                className={classes.icon}
                radius='md'
            />

            <div className={classes.identity}>
                <div className={classes.name} title={title}>
                    {label}
                    {hasDuplicates && <UIPokedexIcons.Duplicate size='xs' ml='xs' style={{ verticalAlign: 'middle' }} />}
                </div>

                <div className={classes.meta}>
                    <Badge variant='default' size='xs'>{generation}</Badge>
                    {ot && otGender !== undefined && tid !== undefined && <span>
                        {t('save.ot')} {ot} <UIGender gender={otGender} /> · {t('details.tid')} {tid}
                    </span>}
                    {language && <span>· {language}</span>}
                </div>
            </div>

            {!loading && actions && <div className={classes.actions}>
                {actions}
            </div>}
        </div>

        <div className={classes.stats}>
            <div className={classes.stat}>
                <div className={classes.statValue}>
                    {ownedCount ?? '-'}
                    {shinyCount !== undefined && shinyCount > 0 && <Group component='span' gap={2} wrap='nowrap' fz='sm' fw={400} c='dimmed'>
                        <UIPokedexIcons.Shiny size='xs' />{shinyCount}
                    </Group>}
                </div>
                <div className={classes.statLabel}>
                    <UIPokedexIcons.Owned size='xs' /> {t('details.owned.title')}
                </div>
            </div>

            <div className={classes.stat}>
                <div className={classes.statValue}>
                    {caughtCount ?? '-'}
                </div>
                <div className={classes.statLabel}>
                    <UIPokedexIcons.Caught size='xs' /> {t('header.dex')}
                </div>
            </div>

            <div className={classes.stat}>
                <div className={classes.statValue}>
                    {playTime ?? '-'}
                </div>
                <div className={classes.statLabel}>
                    <TimerIcon size={12} /> {t('storage.games.sort.play-time')}
                </div>
            </div>
        </div>

        <div className={classes.path}>
            <UIPathLine>{path}</UIPathLine>
        </div>

        <div className={classes.footer}>
            <WithControlsIcons placement='out' icons={controlIcons('open')}>
                <Button
                    variant='default'
                    size='xs'
                    rightSection={<ArrowRightIcon />}
                    loading={loading}
                    title={title}
                    {...focusProps}
                    {...controlProps('open')}
                >
                    {t('action.open')}
                </Button>
            </WithControlsIcons>

            {!loading && editDropdown && <UIPopover
                position='right'
                dropdown={editDropdown}
            >
                <WithControlsIcons placement='out' icons={controlIcons('edit')}>
                    <Button
                        variant='light'
                        color='blue'
                        size='xs'
                        {...controlProps('edit')}
                    >
                        <Group wrap='nowrap' gap='sm'>
                            {hasDuplicates && <CopyIcon color='var(--mantine-color-yellow-4)' />}
                            <PenIcon />
                        </Group>
                    </Button>
                </WithControlsIcons>
            </UIPopover>}
        </div>
    </article>;
};
