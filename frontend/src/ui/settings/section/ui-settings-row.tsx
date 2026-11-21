import type React from 'react';
import classes from '../ui-settings.module.css';

type UISettingsRowProps = {
    icon?: React.ReactNode;
    label: React.ReactNode;
    description?: React.ReactNode;
    /**
     * Id of the input the label points to.
     */
    forInput?: string;
    children?: React.ReactNode;
};

/**
 * Settings row: label and description on the left, control on the right.
 * Rows are separated by 1px borders inside a UISettingsSection.
 */
export const UISettingsRow: React.FC<UISettingsRowProps> = ({ icon, label, description, forInput, children }) => {
    const LabelTag = forInput ? 'label' : 'div';

    return <div className={classes.row}>
        <div className={classes.rowLabel}>
            {icon && <span className={classes.rowIcon}>{icon}</span>}

            <div className={classes.rowText}>
                <LabelTag className={classes.rowTitle} htmlFor={forInput}>{label}</LabelTag>
                {description && <div className={classes.rowDescription}>{description}</div>}
            </div>
        </div>

        {children !== undefined && <div className={classes.rowControl}>
            {children}
        </div>}
    </div>;
};
