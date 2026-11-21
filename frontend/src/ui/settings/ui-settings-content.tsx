import type React from 'react';
import { WithControlsIcons } from '../interaction/controls/icons/with-controls-icons';
import { FocusScope } from '../interaction/focus/scope/focus-scope';
import { usePanelControls } from '../layout/hooks/use-panel-controls';
import classes from './ui-settings.module.css';

type UISettingsContentProps = Pick<React.DOMAttributes<HTMLFormElement>, 'onSubmit'> & {
    title: React.ReactNode;
    description?: React.ReactNode;
    children: React.ReactNode;
    bottom: React.ReactNode;
};

export const UISettingsContent: React.FC<UISettingsContentProps> = ({ title, description, children, bottom, onSubmit }) => {

    const { panelProps, nodeId, childScopeId, controlIcons } = usePanelControls('settings-content');

    return <FocusScope id={childScopeId} parentNodeId={nodeId}>
        <WithControlsIcons placement='out' icons={controlIcons('open')}
            as='form'
            onSubmit={onSubmit as never}
            style={{
                flexGrow: 1,
                maxHeight: '100%',
                minHeight: 0,
                display: 'flex',
                flexDirection: 'column',
                flexWrap: 'nowrap',
                borderRadius: 'var(--mantine-radius-md)',
            }}
            {...panelProps}
        >
            <div className={classes.page}>
                <div className={classes.column}>
                    <header className={classes.pageHeader}>
                        <h1 className={classes.pageTitle}>{title}</h1>
                        {description && <p className={classes.pageDescription}>{description}</p>}
                    </header>

                    {children}
                </div>
            </div>

            {bottom && <div className={classes.bottomBar}>
                {bottom}
            </div>}
        </WithControlsIcons>
    </FocusScope>;
};
