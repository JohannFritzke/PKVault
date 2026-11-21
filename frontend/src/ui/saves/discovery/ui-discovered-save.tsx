import { Badge, Checkbox } from '@mantine/core';
import { CopyIcon, TimerIcon } from 'lucide-react';
import type React from 'react';
import { useTranslate } from '../../../translate/i18n';
import { UIGameIcon } from '../../game-icon/ui-game-icon';
import { WithControlsIcons } from '../../interaction/controls/icons/with-controls-icons';
import { getSelectControl } from '../../interaction/focus-controls/common-controls/select-controls';
import { useFocusControls } from '../../interaction/focus-controls/use-focus-controls';
import { UIPathLine } from '../../path/ui-path-line';
import classes from './ui-discovered-save.module.css';

export type UIDiscoveredSaveProps = {
    path: string;
    label: React.ReactNode;
    imgSrc: string;
    generation: string;
    trainerName: string;
    tid: number;
    playTime: string;
    source?: string;
    checked: boolean;
    alreadyAdded: boolean;
    duplicateOf?: string;
    onToggle: () => void;
};

/** One discovered save: whole card toggles its selection. Already added saves are shown but locked. */
export const UIDiscoveredSave: React.FC<UIDiscoveredSaveProps> = ({
    path, label, imgSrc, generation, trainerName, tid, playTime, source,
    checked, alreadyAdded, duplicateOf, onToggle,
}) => {
    const { t } = useTranslate();

    const disabled = alreadyAdded;

    const { focusProps, controlProps, controlIcons } = useFocusControls({
        scopeNodeId: `discovered_${path}`,
        controls: [
            !disabled && getSelectControl({
                label: t('action.select'),
                action: onToggle,
            }),
        ],
    });

    return <WithControlsIcons placement='out' icons={controlIcons('open')} miw={0}>
        <article
            className={classes.uiDiscoveredSave}
            data-checked={checked}
            data-disabled={disabled}
            title={path}
            {...focusProps}
            {...controlProps('open')}
        >
            <Checkbox
                checked={checked}
                disabled={disabled}
                readOnly
                tabIndex={-1}
                aria-label={String(label)}
                className={classes.checkbox}
            />

            <UIGameIcon src={imgSrc} />

            <div className={classes.body}>
                <div className={classes.name}>
                    <span>{label}</span>
                    <Badge variant='default' size='xs'>{generation}</Badge>
                    {alreadyAdded && <Badge variant='light' color='gray' size='xs'>{t('discovery.badge.added')}</Badge>}
                    {duplicateOf && <Badge variant='light' color='yellow' size='xs' leftSection={<CopyIcon size={10} />} title={duplicateOf}>
                        {t('discovery.badge.duplicate')}
                    </Badge>}
                </div>

                <div className={classes.meta}>
                    <span>{t('save.ot')} {trainerName} · {t('details.tid')} {tid}</span>
                    <span className={classes.time}><TimerIcon size={12} /> {playTime}</span>
                    {source && <span>· {source}</span>}
                </div>

                <div className={classes.path}>
                    <UIPathLine>{path}</UIPathLine>
                </div>
            </div>
        </article>
    </WithControlsIcons>;
};
